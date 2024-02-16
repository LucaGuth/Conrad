FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["StateCore/StateCore.csproj", "StateCore/"]
RUN dotnet restore "StateCore/StateCore.csproj"
COPY . .
WORKDIR "/src/StateCore"
RUN dotnet build "StateCore.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "StateCore.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "StateCore.dll"]
