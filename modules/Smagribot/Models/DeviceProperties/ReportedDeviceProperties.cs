using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Smagribot.Models.DeviceProperties
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum UpdateStatus
    {
        Current,        // There is no pending firmware update. currentFwVersion should match fwVersion from desired properties.
        Downloading,    // Firmware update image is downloading.
        Verifying,      // Verifying image file checksum and any other validations.
        Applying,       // Update to the new image file is in progress.
        Rebooting,      // Device is rebooting as part of update process.
        Error,          // An error occurred during the update process. Additional details should be specified in fwUpdateSubstatus.
        Rolledback      // Update rolled back to the previous version due to an error.
    }
    
    public class CurrentFirmware
    {
        public string CurrentFwVersion { get; set; }
        public string PendingFwVersion { get; set; }
        [JsonConverter(typeof(StringEnumConverter))]
        public UpdateStatus FwUpdateStatus { get; set; }
        public string FwUpdateSubstatus { get; set; }
        public string LastFwUpdateStartTime { get; set; }
        public string LastFwUpdateEndTime { get; set; }
    }
    
    public class ReportedDeviceProperties
    {
        public CurrentFirmware Firmware { get; set; }


        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}