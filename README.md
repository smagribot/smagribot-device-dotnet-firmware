# Smagribot 🌱

Smagribot 🌱 is an open source indoor iot plant sensor for hydroponics.

It takes control of the environmental circumstances for growing vegetables and herbs in your own home. It monitors, among other things, the temperature, humidity and water temperature. The monitored data allows the system to control a grow light and a water pump to optimize the health and growth of the plants from sowing to harvest.

# Device
Device module for Smagribot.
Connects to the Azure IoT Hub, sends telemetry data from the Arduino sensors and manages firmware updates for the Arduino and manages twin properties.

# Hardware
Any device, which can run a .net core 3.1 console app, like the Raspberry Pi 3B+, 4, ...

## Enviroment variables

- `SerialPortName`: Portname for serial communication with the Arduino, default: `/dev/ttyACM0`.
- `SerialBaudRate`: Baudrate for serial communication with the Arduino, default: `9600`.
- `AzureIotHubConnectionString`: Device connection string to Azure IoT Hub, e.g.: `HostName=iothub_name.azure-devices.net;DeviceId=device_name;SharedAccessKey=KeyData`.
- `FirmwareDownloadPath`: Path for the Arudino firmwares to download.
    - Docker default: `/etc/smagribot/serial_firmware`
- `ArduinoCliPath`: Path to arduino-cli tool.
    - Docker default: `/bin/arduino-cli`
    - Is installed in docker image
- `FQBN`: Fully Qualified Board Name (Use [arduino-cli board list](https://arduino.github.io/arduino-cli/commands/arduino-cli_board_list/) to see informations about the connected board(s)), default: `arduino:avr:uno`
- *(optional)* `LogLevel`: `none`, `debug`, `information`. Default is `information`, in debug mode it is `debug`.
- *(optional)* `UseDeviceMock`: `true`. Using mocked device for debugging.

## Docker
### Build for running
```bash
docker build --pull --target publish -t smagribot/devicerunner .
```

Run container:
```bash
docker run --privileged --name smagribot \
    -e "SerialPortName=/dev/cu.wchusbserial14611" \
    -e "SerialBaudRate=9600" \
    -e "AzureIotHubConnectionString=HostName=HUBNAME.azure-devices.net;DeviceId=DEVICEID;SharedAccessKey=KEYDATA" \
    -e "FQBN=arduino:avr:uno" \
    --device=/dev/cu.wchusbserial14611 \
    -v $PWD/fwdownloads:/tmp/serial_firmware \
    -d \
    smagribot/devicerunner
```

Run container with mocks:
```bash
docker run --name smagribot_mock \
	-e "UseDeviceMock=true" \
	-e "UseArduinoCliMock=true" \
    -e "AzureIotHubConnectionString=HostName=HUBNAME.azure-devices.net;DeviceId=DEVICEID;SharedAccessKey=KEYDATA" \
    -v $PWD/fwdownloads:/tmp/serial_firmware \
    -it \
    smagribot/devicerunner
```

All options:
```bash
docker run --privileged --name smagribot \
	-e "UseDeviceMock=true" \
	-e "UseArduinoCliMock=true" \
    -e "SerialPortName=/dev/cu.wchusbserial14611" \
    -e "SerialBaudRate=9600" \
    -e "AzureIotHubConnectionString=HostName=HUBNAME.azure-devices.net;DeviceId=DEVICEID;SharedAccessKey=KEYDATA" \
    -e "FirmwareDownloadPath=/tmp/serial_firmware" \
    -e "FQBN=arduino:avr:uno" \
    --device=/dev/cu.wchusbserial14611 \
    -v $PWD/fwdownloads:/tmp/serial_firmware \
    -d \
    smagribot/devicerunner
```

### Build for tests

Build container:
```bash
docker build --pull --target test -t smagribot/test .
```

Run container:
```bash
docker run --rm -v $PWD/TestResults:/source/Tests/TestResults smagribot/test
```