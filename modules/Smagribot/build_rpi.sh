#!/bin/bash

# Restore packages and build
dotnet restore
dotnet publish -c Release -r linux-arm

# Copy run.sh
cp ./run.sh ./bin/Release/netcoreapp3.1/linux-arm/publish/

# Zip publish folder
zip -rj smagribot.zip ./bin/Release/netcoreapp3.1/linux-arm/publish/

# copy data with scp ./smagribot.zip  pi@192.168.0.10:~/smagribot