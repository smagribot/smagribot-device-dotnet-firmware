using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using Microsoft.Extensions.Logging;

namespace Smagribot.Services.Cloud
{
    public class AzureIoTHubService : AzureIoTHubServiceBase
    {
        private readonly DeviceClient _deviceClient;

        public AzureIoTHubService(ILogger logger, string connectionString) : base(logger)
        {   
            _deviceClient = DeviceClient.CreateFromConnectionString(connectionString, TransportType.Mqtt);
        }

        public override async Task Connect()
        {
            Logger.LogDebug($"Connecting to IoT Hub");
            
            await _deviceClient.OpenAsync().ConfigureAwait(false);

            await _deviceClient.SetDesiredPropertyUpdateCallbackAsync(OnDesiredPropertyChanged, null).ConfigureAwait(false);

            var twin = await _deviceClient.GetTwinAsync();
            await OnDesiredPropertyChanged(twin.Properties.Desired, this);
        }

        public override async Task Disconnect()
        {
            Logger.LogDebug($"Disconnecting to IoT Hub");
            await _deviceClient.CloseAsync().ConfigureAwait(false);
        }

        public override Task UploadData(Stream data, string filename)
        {
            return _deviceClient.UploadToBlobAsync(filename, data);
        }

        protected override Task SendMessage(Message message)
        {
            return _deviceClient.SendEventAsync(message);
        }

        protected override Task UpdateTwin(TwinCollection newTwinData)
        {
            return _deviceClient.UpdateReportedPropertiesAsync(newTwinData);
        }
    }
}