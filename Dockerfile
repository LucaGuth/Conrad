# Use the official .NET Core SDK image as the base image
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copy the .csproj and restore as distinct layers
COPY ["PluginBase/PluginBase.csproj", "PluginBase/"]
COPY ["Plugins/ExamplePlugin/ExamplePluginPackage.csproj", "Plugins/ExamplePlugin/"]
COPY ["Sequencer/Sequencer.csproj", "Sequencer/"]

RUN dotnet restore "PluginBase/PluginBase.csproj"
RUN dotnet restore "Plugins/ExamplePlugin/ExamplePluginPackage.csproj"
RUN dotnet restore "Sequencer/Sequencer.csproj"

# Copy the remaining source code and build the application
COPY . .
WORKDIR "/app"
RUN dotnet build "PluginBase/PluginBase.csproj" -c Release -o /app/build
RUN dotnet build "Plugins/ExamplePlugin/ExamplePluginPackage.csproj" -c Release -o /app/build
RUN dotnet build "Sequencer/Sequencer.csproj" -c Release -o /app/build

RUN dotnet publish -c Release -o out

# Build the runtime image
FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app/out .

# Entry point when the container starts
ENTRYPOINT ["dotnet", "Sequencer.dll"]