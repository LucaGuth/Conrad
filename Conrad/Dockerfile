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
WORKDIR /app
COPY --from=build /app/out/Release/*.dll .
COPY --from=build /app/out/Release/*.runtimeconfig.json .
COPY --from=build /app/out/Release/plugins/*.dll plugins/

# Entry point when the container starts
ENTRYPOINT ["dotnet", "Sequencer.dll", "--plugins", "plugins", "--config", "/app/config/config.json", "--log-level", "verbose"]
