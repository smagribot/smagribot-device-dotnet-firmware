using System;
using Newtonsoft.Json;

namespace Smagribot.Models.DeviceProperties
{
    public class TelemetryConfig
    {
        public double Period { get; set; }
    }

    public class LightSchedule
    {
        public double On { get; set; }
        public double Off { get; set; }

        public override bool Equals(object? obj)
        {
            return obj != null && obj is LightSchedule schedule && schedule.Off == Off && schedule.On == On;
        }

        protected bool Equals(LightSchedule other)
        {
            return On.Equals(other.On) && Off.Equals(other.Off);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(On, Off);
        }
    }

    public class ArduinoSerialFirmware
    {
        public string FwVersion { get; set; }
        public string FwPackageUri { get; set; }
        public string FwPackageCheckValue { get; set; }
    }
    
    public class DesiredDeviceProperties
    {
        public TelemetryConfig TelemetryConfig { get; set; }
        public LightSchedule LightSchedule { get; set; }
        public ArduinoSerialFirmware ArduinoSerialFirmware { get; set; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}