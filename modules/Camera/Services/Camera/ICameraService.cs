using System;
using System.IO;
using System.Threading.Tasks;
using Smagribot.Models.DeviceProperties;

namespace Camera.Services.Camera
{
    public interface ICameraService : IDisposable
    {
        public string SensorName { get; }
        public Task<Stream> TakePicture(ImageConfig imageConfig);
    }
}