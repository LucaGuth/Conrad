# Conrad
Conrad is the acronym for **c**ognitive **o**ptimiser for **n**otifications, **r**ecommendations and **a**utomated **d**ata management. It was developed as part of the advanced software engineering course at the DHBW Stuttgart. Its task is to act as an assistant and help its user throughout the day with various tasks. The system is therefore based on a large language model to select what data needs to be requested and to parse the output of the subsystems to interact with the user. The main interaction takes place through speech via the Discord chat platform.

## Use cases
A big part of the project was to set up an automatic routine so that some alerts and information would be sent to the user automatically, without them having to ask for it. However, it is still possible to interact with Conrad by asking questions directly. Each use cases has three so called monitoring services.

### The four main use cases are as follows:
**Morning use case:**
- Getting information about the weather for the day
- Getting the events the user has that day
- Check the train connection to work

**Culinary use case:**
- Recommend a coffee 
- Recommend recipes for cooking
- Search for nearby restaurants

**Entertainment use case:**
- Search for interesting facts on Wikipedia
- Take a quiz on a random topic
- Search for new upcoming technologies

**Information Use Case:**
- Get the latest news
- Get the latest weather
- Get the share details of a company


## Configuration
The monitoring services all use up APIs of public services, some of them may need a payed plan when using often. The API-Keys, if needed, need to be registered in the configuration File in `Conrad/config/config.json` (generated after the first run).
The Discord user interface must be configured separately in the folder `ConradDiscordAdapter`. Enter all the necessary information in the config such as the discord bot token, the text channel for text input and output, the voice channel, and the userid that the speech-to-text module should listen to. Please keep in mind to rename the config to `config.json`

## Starting Conrad
To maximise runability on multiple device types, Conrad runs behind Docker. All you need is docker with the docker compose plugin. On the first run the system needs to be built, to start the build process type `docker compose build`. If all goes well, you can start the software with `docker compose up -d`.

## Stopping Conrad
To stop conrad type: `docker compose down`

## System requirements
With everything running on the local computer, a more powerful system is required. During our development, we ran Conrad on an Ubuntu workstation with 64GB of RAM and 16GB of VRAM in the graphics card. Although these specs are not bad, a query took anywhere from 30 seconds to almost 2 minutes, depending on what the input was. 



