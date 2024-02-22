# Use the Microsoft official .NET 8.0 SDK image to build the application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /app

# Copy the source code
COPY . ./

# Publish the application to a folder for deployment
RUN dotnet build

# Use the .NET 8.0 runtime image for the final image
#FROM mcr.microsoft.com/dotnet/runtime:8.0
FROM mcr.microsoft.com/dotnet/sdk:8.0
WORKDIR /app

# Copy the published application from the build environment to the final image
COPY --from=build-env /app/out .

# Set the command to run your application
#ENTRYPOINT ["dotnet", "/app/out/Sequencer.dll"]

# Start the container with bash so it doesn't exit
ENTRYPOINT ["dotnet", "run", "--project", "Sequencer"]