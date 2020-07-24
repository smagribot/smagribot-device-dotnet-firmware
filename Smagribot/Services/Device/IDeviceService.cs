using System;
using System.Threading.Tasks;
using Smagribot.Models.Messages;
using Smagribot.Models.Methods;

namespace Smagribot.Services.Device
{
    public interface IDeviceService
    {
        Task Connect();
        Task Disconnect();
        Task<DeviceStatus> GetStatus();
        Task<Version> GetFirmware();

        Task<bool> SetFan(Fan fan);
        Task<bool> SetRelay(Relay relay);
    }
}