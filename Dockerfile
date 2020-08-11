# https://hub.docker.com/_/microsoft-dotnet-core
FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS build
WORKDIR /source

# copy csproj and restore as distinct layers
COPY Smagribot/*.csproj Smagribot/
RUN dotnet restore Smagribot/Smagribot.csproj

# copy and build app and libraries
COPY Smagribot/ Smagribot/
WORKDIR /source/Smagribot
RUN dotnet build -c release --no-restore

# test stage -- exposes optional entrypoint
# target entrypoint with: docker build --target test
FROM build AS test
WORKDIR /source/Tests
COPY Tests/ .
ENTRYPOINT ["dotnet", "test", "--logger:trx"]

FROM build AS publish
RUN dotnet publish -c release -o /app --no-build

# final stage/image
FROM mcr.microsoft.com/dotnet/core/runtime:3.1

ENV FirmwareDownloadPath="/etc/smagribot/serial_firmware" \
    ArduinoCliPath="/bin/arduino-cli"

RUN mkdir -p "/etc/smagribot/serial_firmware"

# Install arduino-cli
RUN curl -fsSL https://raw.githubusercontent.com/arduino/arduino-cli/master/install.sh | BINDIR=/bin sh

WORKDIR /app
COPY --from=publish /app .
ENTRYPOINT ["dotnet", "Smagribot.dll"]