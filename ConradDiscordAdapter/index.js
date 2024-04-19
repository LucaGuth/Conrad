const Discord = require('discord.js');
const { GatewayIntentBits } = require('discord-api-types/v10');
const DiscordVoice = require('@discordjs/voice');
const {entersState, VoiceConnectionStatus, EndBehaviorType, createAudioPlayer, createAudioResource, StreamType} = require("@discordjs/voice");
const { discord, openAIKey, conrad, inputRequests } = require('./config.json')
const prism = require('prism-media');
const {createWriteStream} = require("node:fs");
const {pipeline} = require("node:stream");
const ffmpeg = require('fluent-ffmpeg');
const fs = require('fs');
const net = require('net');
const http = require('http');
const { createReadStream } = require('node:fs');

const httpServer = http.createServer((req, res) => {
    // check if the request is plain text
    if (req.headers['content-type'] === 'text/plain') {
        let body = '';
        req.on('data', (chunk) => {
            body += chunk.toString();
        });
        req.on('end', () => {
            sendDiscordMessage(body);
        });
        res.end('Message received');
    } else if (req.headers['content-type'] === 'audio/ogg' || req.headers['content-type'] === 'audio/mpeg') {
        const fileStream = fs.createWriteStream('/tmp/uploaded_file.ogg');
        req.pipe(fileStream);
        
        fileStream.on('finish', () => {
            playAudio('/tmp/uploaded_file.ogg');
            res.writeHead(200, {'Content-Type': 'text/plain'});
            res.end('File uploaded successfully');
        });
    } else if (req.headers['content-type'] === 'application/octet-stream') {
        const fileStream = fs.createWriteStream('/tmp/uploaded_file.pcm');
        req.pipe(fileStream);
        fileStream.on('finish', () => {
            res.writeHead(200, {'Content-Type': 'text/plain'});
            res.end('File uploaded successfully');
            
            const command = ffmpeg()
                .input(fileStream.path)
                .inputFormat('s16le')
                .audioChannels(1)
                .inputOptions('-sample_rate 22050')
                .inputOptions('-channel_layout mono')
                .audioCodec('libvorbis') 
                .audioQuality(4)
                .outputOptions('-f ogg')
                .output("/tmp/output.ogg")
                .on('end', () => {
                    console.log('Conversion complete');
                    playAudio('/tmp/output.ogg');
                })
                .on('error', (err) => {
                    console.error('Error during conversion:', err);
                });

            command.run();

        });
    } else {
        res.writeHead(400, {'Content-Type': 'text/plain'});
        res.end('Invalid request');
    }
});

httpServer.listen(inputRequests.port, inputRequests.host, () => {
    console.log(`Input-Server running at ${inputRequests.host}:${inputRequests.port}/`);
});

const client = new Discord.Client({
    intents: [GatewayIntentBits.GuildVoiceStates, GatewayIntentBits.GuildMessages, GatewayIntentBits.Guilds],
});

client.on('ready', async () => {
    console.log('ConradDiscordAdapter is ready');
    const channel = client.channels.cache.get(discord.voiceChannel);

    await createConnection(channel);
});

client.on('messageCreate', async (message) => {
    if (message.author.bot) return;
    if (message.channel.id !== discord.textChannel) return;
    sendToConrad(message.content);
});

function sendDiscordMessage(message) {
    if (message.length > 2000) {
        for (let i = 0; i < message.length; i += 2000) {
            client.channels.cache.get(discord.textChannel).send(message.substring(i, i + 2000));
        }
    } else {
	client.channels.cache.get(discord.textChannel).send(message);
    }
	promptsRunning--;
}

function playAudio(file) {
    const player = createAudioPlayer();

    const resource = createAudioResource(createReadStream(file, {
        inputType: StreamType.OggOpus,
    }));

    player.play(resource);

    connection.subscribe(player);
}

let connection;

async function createConnection(channel) {
    connection = DiscordVoice.joinVoiceChannel({
        channelId: channel.id,
        guildId: channel.guild.id,
        adapterCreator: channel.guild.voiceAdapterCreator,
        selfDeaf: false,
        selfMute: false,
        group: client.user.id,
    });

    await entersState(connection, VoiceConnectionStatus.Ready, 20e3);

    connection.receiver.speaking.on('start', async (userId) => {
        if (!recording) {
            recordUser(connection);
        }
    });

    connection.on('stateChange', async (oldState, newState) => {
        if (newState.status === "signalling") {
            connection.configureNetworking();
        }
    });
}

let recording = false;

function recordUser(connection) {
    console.log("Conrad is listening...");

    const opusStream = connection.receiver.subscribe(discord.user, {
        end: {
            behavior: EndBehaviorType.AfterSilence,
            duration: 1000,
        }
    });

    const oggStream = new prism.opus.OggLogicalBitstream({
        opusHead: new prism.opus.OpusHead({
            channelCount: 2,
            sampleRate: 48000,
        }),
        pageSizeControl: {
            maxPackets: 10,
        },
    });

    if (!fs.existsSync('recordings')) fs.mkdirSync('recordings');
    const filename = `./recordings/${Date.now()}-289046908809510912.ogg`;
    const out = createWriteStream(filename);
    console.log(`👂 Started recording ${filename}`);
    recording = true;

    pipeline(opusStream, oggStream, out, (err) => {
        if (err) {
            console.warn(`❌ Error recording file ${filename} - ${err.message}`);
        } else {
            console.log(`✅ Recorded ${filename}`);
            convertOggToMp3(filename, filename.replace('.ogg', '.mp3'));
        }
        recording = false;
    });
    connection.receiver.speaking.on('start', async (userId) => {
        if (!recording) {
            recordUser(connection);
        }
    });

}

function convertOggToMp3(oggFilePath, mp3FilePath) {
    const outStream = createWriteStream(mp3FilePath);
    ffmpeg()
        .input(oggFilePath)
        .audioQuality(96)
        .toFormat("mp3")
        .on('error', error => console.log(`Encoding Error: ${error.message}`))
        .on('exit', () => console.log('Audio recorder exited'))
        .on('close', () => console.log('Audio recorder closed'))
        .on('end', async () => {
		console.log('Audio Transcoding succeeded !');
		const { exec } = require('child_process');

		exec(`. /python_venv/bin/activate && whisper --model small.en --model_dir models --language en --output_format txt ${mp3FilePath}`, (err, stdout, stderr) => {
			if (err) {
				console.log(err);
				return;
			}

			const pattern = /\[\d{2}:\d{2}.\d{3} --> \d{2}:\d{2}.\d{3}\] /g;

			// Replace the matched pattern with an empty string
			const result = stdout.replace(pattern, '');
			sendToConrad(result);
		});

        })
        .pipe(outStream, { end: true });
}

let promptsRunning = 0;
async function sendTypingStatus() {
	if (promptsRunning > 0) {
		client.channels.cache.get(discord.textChannel).sendTyping();
		setTimeout(() => sendTypingStatus(), 5000);
	}
}

async function sendToConrad(text, count = 0) {
    const networkClient = new net.Socket();

    networkClient.connect(conrad.port, conrad.ip, () => {
        // Send plain text message
        networkClient.write(text);
	    promptsRunning++;
	    sendTypingStatus();
    });

    networkClient.on('error', (error) => {
        if (count >= 5) {
            console.log('Connection refused too many times, giving up');
            client.channels.cache.get(discord.textChannel).send('Conrad is not available right now, please try again later!');
	    promptsRunning--;
            return;
        }
        if (error.code === 'ECONNREFUSED') {
            console.log('Connection refused, trying again in 2 seconds');
            count++;
            setTimeout(() => sendToConrad(text, count), 2000);
        }
    });

}

client.login(discord.token);
