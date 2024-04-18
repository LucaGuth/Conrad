FROM node:20.11.0-bookworm

# Set the working directory
WORKDIR /app

# Install ffmpeg
RUN apt-get update && apt-get install -y ffmpeg

# Copy the package.json and package-lock.json
COPY package*.json ./

# Install the dependencies
RUN npm install

# Install whisper openai
RUN apt-get install python3-full -y
RUN python3 -m venv /python_venv
RUN chmod +x /python_venv/bin/activate
RUN /python_venv/bin/activate
RUN /python_venv/bin/pip3 install openai-whisper

# Start the application
CMD ["node", "index.js"]
