using System;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Camera.Services.Camera;
using Microsoft.Extensions.Logging;
using Smagribot.Models.DeviceProperties;
using Smagribot.Services.Cloud;
using Smagribot.Services.Scheduler;
using Smagribot.Services.Utils;

namespace Camera
{
    public class Runner : IDisposable
    {
        private readonly ILogger _logger;
        private readonly ISchedulerProvider _schedulerProvider;
        private readonly ICloudService _cloudService;
        private readonly ICameraService _cameraService;
        private readonly IClockService _clockService;
        
        private TimeSpan _currentTimerInterval;

        private ImageConfig _currentImageConfig = new ImageConfig
        {
            Height = 480,
            Width = 640,
            EncodingFormat = EncodingFormat.BMP,
            PixelFormat = PixelFormat.RGBA,
            ShutterSpeed = 0, //Auto
            ISO = 0, // Auto
            Quality = 100
        };
        
        private readonly CompositeDisposable _compositeDisposable = new CompositeDisposable();
        private IDisposable _timedUpdateDisposable;

        public Runner(ILogger logger, ISchedulerProvider schedulerProvider, ICloudService cloudService, ICameraService cameraService, IClockService clockService)
        {
            _logger = logger;
            _schedulerProvider = schedulerProvider;
            _cloudService = cloudService;
            _cameraService = cameraService;
            _clockService = clockService;
        }

        public void Run()
        {
            var connectedObservable = Observable.FromAsync(async () => await _cloudService.Connect());

            SendStatusAndHearForPeriodUpdates(connectedObservable);
        }
        
        private void SendStatusAndHearForPeriodUpdates(IObservable<Unit> connectedObservable)
        {
            // Get TimelapseConfig.Period from reported properties to update StartIntervalledStatusUpdate with new TimeSpan
            var newIntervalObservable = _cloudService.GetDesiredProperties()
                .Where(properties => properties?.TimelapseConfig != null)
                .Do(properties => _currentImageConfig = properties.TimelapseConfig.ImageConfig)
                .Select(properties => properties.TimelapseConfig.Period)
                .Where(period => period > 0)
                .Select(TimeSpan.FromSeconds)
                // When device and cloud are connected, start once with default interval, in case reported properties
                // doesn't contain TimelapseConfig.Period or they aren't reported yet
                .Merge(connectedObservable.Select(_ => TimeSpan.FromMinutes(15)))
                .Where(timespan => timespan != _currentTimerInterval)
                .Do(timespan => _currentTimerInterval = timespan)
                .SubscribeOn(_schedulerProvider.NewThread)
                .Subscribe(timespan =>
                    {
                        _logger.LogInformation($"Setup new interval for timelapse interval. Interval is {timespan.TotalMinutes} minutes");
                        StartIntervalledStatusUpdate(timespan);
                    },
                    err => { _logger.LogError($"Got error while listining for TimelapseConfig.Period: {err}"); },
                    () => { _logger.LogInformation("Listing for TimelapseConfig.Period finished"); });

            _compositeDisposable.Add(newIntervalObservable);
        }
        
        private void StartIntervalledStatusUpdate(TimeSpan timespan)
        {
            _timedUpdateDisposable?.Dispose();
            _timedUpdateDisposable = Observable.Interval(timespan, _schedulerProvider.NewThread)
                .Merge(Observable.Return(0L))
                .SelectMany(_ => _cameraService.TakePicture(_currentImageConfig))
                .SelectMany(async data => { 
                    await _cloudService.UploadData(data, GetFilename());
                    return Unit.Default;
                })
                .SubscribeOn(_schedulerProvider.NewThread)
                .Subscribe(status => { _logger.LogInformation($"Uploaded picture"); },
                    err => { _logger.LogError($"Uploaded picture interval got error: {err}"); },
                    () => { _logger.LogInformation("Uploaded pictures interval finished"); });
        }

        private static string GetFilename()
        {
            return $"timelapse_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.jpg";
        }

        // private IObservable<(Stream, string, int)> SendStatusToCloudWithRetry(Stream data, string filename)
        // {
        //     return Observable.FromAsync(() => _cloudService.UploadData(data, filename))
        //         .Select(_ => (data, filename))
        //         .RetryWhen(observable =>
        //             observable
        //                 .Do(ex => _logger.LogWarning($"SendStatusMessage throw {ex} trying again"))
        //                 .Zip(Observable.Return(0).Delay(TimeSpan.FromSeconds(30), _schedulerProvider.NewThread),
        //                     (exception, i) => i)
        //                 .Do(_ => _logger.LogInformation("SendStatusMessage trying again"))
        //         );
        // }
        
        public void Dispose()
        {
            _compositeDisposable?.Dispose();
            _timedUpdateDisposable?.Dispose();
        }
    }
}