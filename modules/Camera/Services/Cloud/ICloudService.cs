using System;
using System.IO;
using System.Threading.Tasks;
using Smagribot.Models.DeviceProperties;

namespace Smagribot.Services.Cloud
{
    public interface ICloudService
    {
        Task Connect();
        Task Disconnect();
        Task UploadData(Stream data, string filename);
        Task UpdateProperties(ReportedDeviceProperties updatedProperties);
        IObservable<DesiredDeviceProperties> GetDesiredProperties();
    }
}