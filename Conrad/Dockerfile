# Use the official .NET Core SDK image as the base image
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copy the .csproj and restore as distinct layers
COPY PluginBase/PluginBase.csproj PluginBase/
COPY Sequencer/Sequencer.csproj Sequencer/

RUN dotnet restore PluginBase/PluginBase.csproj
RUN dotnet restore Sequencer/Sequencer.csproj

# Copy the remaining source code and build the application
COPY . .
RUN dotnet build ./Conrad.sln -c Release

# Build the runtime image
FROM mcr.microsoft.com/dotnet/runtime:8.0

RUN apt-get update
RUN apt-get install python3-full -y
RUN python3 -m venv /python_venv
RUN chmod +x /python_venv/bin/activate
RUN /python_venv/bin/activate
RUN /python_venv/bin/pip3 install piper-tts

RUN apt-get install curl -y

WORKDIR /app

RUN curl --create-dirs -L https://huggingface.co/rhasspy/piper-voices/resolve/c3bf31a7c50ea738281519ca86ba2ee0f50a1882/en/en_US/ryan/high/en_US-ryan-high.onnx?download=true -o piper_models/en_US-ryan-high.onnx
RUN curl --create-dirs -L https://huggingface.co/rhasspy/piper-voices/resolve/c3bf31a7c50ea738281519ca86ba2ee0f50a1882/en/en_US/ryan/high/en_US-ryan-high.onnx.json?download=true -o piper_models/en_US-ryan-high.onnx.json

COPY --from=build /app/out/Release/*.dll .
COPY --from=build /app/out/Release/*.runtimeconfig.json .
COPY --from=build /app/out/Release/plugins/*.dll plugins/

# Entry point when the container starts
ENTRYPOINT ["dotnet", "Sequencer.dll", "--plugins", "plugins", "--config", "/app/config/config.json"]
