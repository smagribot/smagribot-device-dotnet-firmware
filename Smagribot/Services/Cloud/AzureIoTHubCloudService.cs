using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Smagribot.Models.DeviceProperties;
using Smagribot.Models.Messages;
using Smagribot.Models.Methods;

namespace Smagribot.Services.Cloud
{
    public class AzureIoTHubCloudService : ICloudService
    {
        private readonly ILogger _logger;
        private readonly DeviceClient _deviceClient;
        
        private readonly Subject<DesiredDeviceProperties> _desiredDevicePropertiesSubject = new Subject<DesiredDeviceProperties>();
        private readonly Subject<Relay> _relaySubject = new Subject<Relay>();
        private readonly Subject<Fan> _fanSubject = new Subject<Fan>();
        
        public AzureIoTHubCloudService(ILogger logger, string connectionString)
        {
            _logger = logger;
            
            _deviceClient = DeviceClient.CreateFromConnectionString(connectionString, TransportType.Mqtt);
        }
        
        private Task<MethodResponse> SetRelayMethod(MethodRequest methodRequest, object userContext)
        {
            _logger.LogDebug($"{nameof(SetRelayMethod)} was called");

            var relay = JsonConvert.DeserializeObject<Relay>(methodRequest.DataAsJson);
            _relaySubject.OnNext(relay);
            
            //TODO: Needs proper result!
            return Task.FromResult(new MethodResponse(new byte[0], 200));
        }
        
        private Task<MethodResponse> SetFanMethod(MethodRequest methodRequest, object userContext)
        {
            _logger.LogDebug($"{nameof(SetFanMethod)} was called");
            
            var fan = JsonConvert.DeserializeObject<Fan>(methodRequest.DataAsJson);
            _fanSubject.OnNext(fan);

            //TODO: Needs proper result!
            return Task.FromResult(new MethodResponse(new byte[0], 200));
        }

        private Task OnDesiredPropertyChanged(TwinCollection desiredProperties, object userContext)
        {
            _logger.LogDebug($"Desired property changed:\n{desiredProperties.ToJson()}");

            var desiredDeviceProperties = JsonConvert.DeserializeObject<DesiredDeviceProperties>(desiredProperties.ToJson());
            _desiredDevicePropertiesSubject.OnNext(desiredDeviceProperties);

            return Task.CompletedTask;
        }

        public async Task Connect()
        {
            _logger.LogDebug($"Connecting to IoT Hub");
            
            await _deviceClient.OpenAsync().ConfigureAwait(false);
            
            await _deviceClient.SetMethodHandlerAsync("SetRelay", SetRelayMethod, null).ConfigureAwait(false);
            await _deviceClient.SetMethodHandlerAsync("SetFan", SetFanMethod, null).ConfigureAwait(false);

            await _deviceClient.SetDesiredPropertyUpdateCallbackAsync(OnDesiredPropertyChanged, null).ConfigureAwait(false);

            var twin = await _deviceClient.GetTwinAsync();
            await OnDesiredPropertyChanged(twin.Properties.Desired, this);
        }

        public async Task Disconnect()
        {
            _logger.LogDebug($"Disconnecting to IoT Hub");
            await _deviceClient.CloseAsync().ConfigureAwait(false);
        }

        public async Task SendStatusMessage(DeviceStatus status)
        {
            _logger.LogDebug($"Sending status to IoT Hub:\n{status}");
            var eventMessage = JsonConvert.SerializeObject(status);
            await _deviceClient.SendEventAsync(new Message(Encoding.UTF8.GetBytes(eventMessage))).ConfigureAwait(false);
        }

        public async Task SendEvent(string eventMessage)
        {
            _logger.LogDebug($"Sending event to IoT Hub:\n{eventMessage}");
            await _deviceClient.SendEventAsync(new Message(Encoding.UTF8.GetBytes(eventMessage))).ConfigureAwait(false);
        }

        public async Task UpdateProperties(ReportedDeviceProperties updatedProperties)
        {
            var propertiesAsJson = JsonConvert.SerializeObject(updatedProperties, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            });
            _logger.LogDebug($"Sending updated properties to IoT Hub:\n{propertiesAsJson}");
            var newTwinData = new TwinCollection(propertiesAsJson);
            await _deviceClient.UpdateReportedPropertiesAsync(newTwinData).ConfigureAwait(false);
        }

        public IObservable<Relay> SetRelay()
        {
            return _relaySubject.Publish().RefCount();
        }

        public IObservable<Fan> SetFan()
        {
            return _fanSubject.Publish().RefCount();
        }

        public IObservable<string> CloudMessage()
        {
            return Observable.Empty<string>();
        }

        public IObservable<DesiredDeviceProperties> GetDesiredProperties()
        {
            return _desiredDevicePropertiesSubject.Publish().RefCount();
        }
    }
}