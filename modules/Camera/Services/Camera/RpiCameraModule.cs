using System.IO;
using System.Threading.Tasks;
using Camera.Extensions;
using MMALSharp;
using MMALSharp.Common.Utility;
using MMALSharp.Handlers;
using Smagribot.Models.DeviceProperties;

namespace Camera.Services.Camera
{
    public class RpiCameraModule : ICameraService
    {
        private readonly MMALCamera _cam;

        public RpiCameraModule()
        {
            _cam = MMALCamera.Instance;
        }

        public async Task<Stream> TakePicture(ImageConfig imageConfig)
        {
            MMALCameraConfig.StillResolution = new Resolution(imageConfig.Width, imageConfig.Height); // Default is 1280 x 720.
            MMALCameraConfig.ShutterSpeed = imageConfig.ShutterSpeed; // Default is 0 (auto).
            MMALCameraConfig.ISO = imageConfig.ISO; //  Default is 0 (auto).
            
            _cam.ConfigureCameraSettings();
            
            var encodingType = imageConfig.EncodingFormat.CreateMMALEncoding();
            var pixelFormat = imageConfig.PixelFormat.CreateMMALEncoding();
            
            var captureHandler = new MemoryStreamCaptureHandler();
            await _cam.TakePicture(captureHandler, encodingType, pixelFormat);
            return captureHandler.CurrentStream;
        }

        public string SensorName => _cam?.Camera.CameraInfo.SensorName;
        
        public void Dispose()
        {
            _cam?.Cleanup();
        }
    }
}