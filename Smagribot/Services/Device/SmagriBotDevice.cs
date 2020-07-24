using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Smagribot.Models.Messages;
using Smagribot.Models.Methods;
using Smagribot.Services.DeviceCommunication;
using Smagribot.Services.Parser;

namespace Smagribot.Services.Device
{
    public class SmagriBotDevice : IDeviceService
    {
        private ILogger _logger;
        private readonly ICommunicationService _communicationService;
        private readonly IDeviceResultParser _deviceResultParser;

        public SmagriBotDevice(ILogger logger, ICommunicationService communicationService,
            IDeviceResultParser deviceResultParser)
        {
            _logger = logger;
            _communicationService = communicationService;
            _deviceResultParser = deviceResultParser;
        }

        public Task Connect()
        {
            return _communicationService.Start();
        }

        public Task Disconnect()
        {
            return _communicationService.Stop();
        }

        public async Task<DeviceStatus> GetStatus()
        {
            var status = new DeviceStatus();
            
            await AddFill(status);
            await AddHumidityAndTemperature(status);
            await AddWaterTemp(status);
            //await AddFanSpeed(status);

            return status;
        }

        public async Task<Version> GetFirmware()
        {
            var fwResult = await _communicationService.Send("getfw").ConfigureAwait(false);
            return _deviceResultParser.ParseVersion(fwResult);
        }

        private async Task AddHumidityAndTemperature(DeviceStatus status)
        {
            var dhtResult = await _communicationService.Send("getdht").ConfigureAwait(false);
            var splittedDhtResult = dhtResult.Split(" ");
            status.Humidity = _deviceResultParser.ParseFloat(splittedDhtResult[0]);
            status.Temp = _deviceResultParser.ParseFloat(splittedDhtResult[1]);
        }
        
        private async Task AddFill(DeviceStatus status)
        {
            var fillResult = await _communicationService.Send("getfill").ConfigureAwait(false);
            status.Fill = _deviceResultParser.ParseBool(fillResult);
        }
        
        private async Task AddWaterTemp(DeviceStatus status)
        {
            var waterTempResult = await _communicationService.Send("getwatertmp").ConfigureAwait(false);
            status.WaterTemp = _deviceResultParser.ParseFloat(waterTempResult);
        }

        private async Task AddFanSpeed(DeviceStatus status)
        {
            var fanResult = await _communicationService.Send("getfan").ConfigureAwait(false);
            status.FanSpeed = _deviceResultParser.ParseInt(fanResult);
        }
        
        public async Task<bool> SetFan(Fan fan)
        {
            var fanResult = await _communicationService.Send($"setfan {fan.Speed}").ConfigureAwait(false);
            return _deviceResultParser.ParseCommand(fanResult);
        }

        public async Task<bool> SetRelay(Relay relay)
        {
            var message = $"setrelay {relay.Number} {_deviceResultParser.ParseBool(relay.On)}";
            var result = await _communicationService.Send(message).ConfigureAwait(false);
            return  _deviceResultParser.ParseCommand(result);
        }
    }
}