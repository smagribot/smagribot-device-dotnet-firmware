using System;
using System.Threading.Tasks;
using Smagribot.Models.DeviceProperties;
using Smagribot.Models.Messages;
using Smagribot.Models.Methods;

namespace Smagribot.Services.Cloud
{
    public interface ICloudService
    {
        Task Connect();
        Task Disconnect();

        Task SendStatusMessage(DeviceStatus status);
        Task UpdateProperties(ReportedDeviceProperties updatedProperties);

        IObservable<Relay> SetRelay();
        IObservable<Fan> SetFan();

        IObservable<string> CloudMessage();
        IObservable<DesiredDeviceProperties> GetDesiredProperties();
    }
}