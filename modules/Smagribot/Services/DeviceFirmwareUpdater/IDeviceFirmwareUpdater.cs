using System;
using Smagribot.Models.DeviceProperties;

namespace Smagribot.Services.DeviceFirmwareUpdater
{
    public interface IDeviceFirmwareUpdater
    {
        public IObservable<CurrentFirmware> UpdateFirmware { get; }
    }
}