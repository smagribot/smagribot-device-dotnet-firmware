using System;
using System.IO;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Smagribot.Models.DeviceProperties;

namespace Smagribot.Services.Cloud
{
    public abstract class AzureIoTHubServiceBase : ICloudService
    {
        protected readonly ILogger Logger;
        private readonly Subject<DesiredDeviceProperties> _desiredDevicePropertiesSubject = new Subject<DesiredDeviceProperties>();

        protected AzureIoTHubServiceBase(ILogger logger)
        {
            Logger = logger;
        }
        
        public abstract Task Connect();
        public abstract Task Disconnect();
        
        public abstract Task UploadData(Stream data, string filename);

        protected abstract Task SendMessage(Message message);

        protected abstract Task UpdateTwin(TwinCollection newTwinData);

        public Task SendEvent(string eventMessage)
        {
            Logger.LogDebug($"Sending event to IoT Hub:\n{eventMessage}");
            var message = new Message(Encoding.UTF8.GetBytes(eventMessage));
            return SendMessage(message);
        }
        
        public Task UpdateProperties(ReportedDeviceProperties updatedProperties)
        {
            var propertiesAsJson = JsonConvert.SerializeObject(updatedProperties, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            });
            Logger.LogDebug($"Sending updated properties to IoT Hub:\n{propertiesAsJson}");
            var newTwinData = new TwinCollection(propertiesAsJson);
            return UpdateTwin(newTwinData);
        }

        protected Task OnDesiredPropertyChanged(TwinCollection desiredProperties, object userContext)
        {
            Logger.LogDebug($"Desired property changed:\n{desiredProperties.ToJson()}");

            var desiredDeviceProperties =
                JsonConvert.DeserializeObject<DesiredDeviceProperties>(desiredProperties.ToJson());
            _desiredDevicePropertiesSubject.OnNext(desiredDeviceProperties);

            return Task.CompletedTask;
        }

        public IObservable<DesiredDeviceProperties> GetDesiredProperties()
        {
            return _desiredDevicePropertiesSubject.Publish().RefCount();
        }
    }
}