FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER app
WORKDIR /app

USER root
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

USER app
WORKDIR /app

EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release

WORKDIR /src

COPY ["TorrentManagerAPI/TorrentManagerAPI.csproj", "TorrentManagerAPI/"]

RUN dotnet restore "./TorrentManagerAPI/TorrentManagerAPI.csproj"

COPY . .

WORKDIR "/src/TorrentManagerAPI"

RUN dotnet build "./TorrentManagerAPI.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./TorrentManagerAPI.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "TorrentManagerAPI.dll"]
