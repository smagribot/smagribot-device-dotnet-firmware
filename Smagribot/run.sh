#!/bin/sh

export SerialPortName="/dev/ttyACM0"
export SerialBaudRate="9600"
export AzureIotHubConnectionString="HostName=IOTHUBNAME.azure-devices.net;DeviceId=DEVICEID;SharedAccessKey=ACCESSKEY"
export FirmwareDownloadPath="/home/pi/Downloads"
export ArduinoCliPath="/usr/bin/arduino-cli"
export FQBN="arduino:avr:uno"
./Smagribot