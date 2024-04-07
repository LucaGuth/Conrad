const {EndBehaviorType} = require("@discordjs/voice");

const Discord = require('discord.js');
const { GatewayIntentBits } = require('discord-api-types/v10');
const DiscordVoice = require('@discordjs/voice');
const {entersState, VoiceConnectionStatus} = require("@discordjs/voice");
const { discord, openAIKey, conrad } = require('./config.json')
const prism = require('prism-media');
const {createWriteStream} = require("node:fs");
const {pipeline} = require("node:stream");
const ffmpeg = require('fluent-ffmpeg');
const OpenAI = require("openai");
const fs = require('fs');
const net = require('net');

const openai = new OpenAI({apiKey: openAIKey});

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

async function createConnection(channel) {
    const connection = DiscordVoice.joinVoiceChannel({
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
            const transcription = await openai.audio.transcriptions.create({
                file: fs.createReadStream(mp3FilePath),
                model: "whisper-1",
                language: "de"
            });

            console.log(transcription.text);
            sendToConrad(transcription.text)
        })
        .pipe(outStream, { end: true });
}

async function sendToConrad(text, count = 0) {
    // send text over tcp to 127.0.0.1:4000
    const networkClient = new net.Socket();

    networkClient.connect(conrad.port, conrad.ip, () => {
        console.log('Connected to server');

        // Send plain text message
        networkClient.write(text);
    });

    networkClient.on('error', (error) => {
        if (count >= 5) {
            console.log('Connection refused too many times, giving up');
            client.channels.cache.get(discord.textChannel).send('Conrad is not available right now, please try again later!');
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