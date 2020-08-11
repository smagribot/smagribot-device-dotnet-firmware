#nullable enable
using System;
using System.IO;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Client.Transport.Mqtt;
using Microsoft.Azure.Devices.Shared;
using Microsoft.Extensions.Logging;

namespace Smagribot.Services.Cloud
{
    public class AzureIoTEdgeService : AzureIoTHubServiceBase
    {
        private ModuleClient _ioTHubModuleClient;
        private readonly string? _containerName;
        private readonly string? _connectionString;

        public AzureIoTEdgeService(ILogger logger) : base(logger)
        {
            _containerName = Environment.GetEnvironmentVariable("BLOB_CONTAINERNAME");
            _connectionString = Environment.GetEnvironmentVariable("BLOB_CONNECTIONSTRING");
        }   
        
        public override async Task Connect()
        {
            Logger.LogDebug($"Connecting to IoT Hub Edge");
            var mqttSetting = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only);
            ITransportSettings[] settings = { mqttSetting };
            
            _ioTHubModuleClient = await ModuleClient.CreateFromEnvironmentAsync(settings);
            await _ioTHubModuleClient.OpenAsync().ConfigureAwait(false);

            await _ioTHubModuleClient.SetDesiredPropertyUpdateCallbackAsync(OnDesiredPropertyChanged, null).ConfigureAwait(false);

            var twin = await _ioTHubModuleClient.GetTwinAsync();
            await OnDesiredPropertyChanged(twin.Properties.Desired, this);
        }

        public override async Task Disconnect()
        {
            Logger.LogDebug($"Disconnecting to IoT Hub");
            await _ioTHubModuleClient.CloseAsync().ConfigureAwait(false);
        }

        public override async Task UploadData(Stream data, string filename)
        {
            // Since module client can't directly upload via iot hub, we have to do it by hand
            // Could also be done with blob module and sync to azure blob?
            Logger.LogDebug($"Uploading ${filename} to blob storage");
            var blobServiceClient = new BlobServiceClient(_connectionString);
            var containerClient = blobServiceClient.GetBlobContainerClient(_containerName);

            var blobClient = containerClient.GetBlobClient(filename);
            await blobClient.UploadAsync(data).ConfigureAwait(false);
            data.Dispose();
        }

        protected override Task SendMessage(Message message)
        {
            return _ioTHubModuleClient.SendEventAsync(message);
        }

        protected override Task UpdateTwin(TwinCollection newTwinData)
        {
            return _ioTHubModuleClient.UpdateReportedPropertiesAsync(newTwinData);
        }
    }
}