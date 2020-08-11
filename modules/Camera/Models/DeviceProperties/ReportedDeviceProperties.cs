using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Smagribot.Models.DeviceProperties
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum CameraStatus
    {
        Working,        // Camera is working.
        Error,          // An error occurred during the camera process. Additional details should be specified in fwUpdateSubstatus.
    }
    
    public class CameraInfo
    {
        public string SensorName { get; set; }
        [JsonConverter(typeof(StringEnumConverter))]
        public CameraStatus CameraStatus { get; set; }
        public string CameraSubstatus { get; set; }
    }
    
    public class ReportedDeviceProperties
    {
        public CameraInfo CameraInfo { get; set; }


        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}