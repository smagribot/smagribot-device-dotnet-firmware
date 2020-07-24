using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Microsoft.Extensions.Logging;
using Smagribot.Models.DeviceProperties;
using Smagribot.Models.Messages;
using Smagribot.Models.Methods;
using Smagribot.Services.Cloud;
using Smagribot.Services.Device;
using Smagribot.Services.DeviceFirmwareUpdater;
using Smagribot.Services.Scheduler;
using Smagribot.Services.Utils;

namespace Smagribot
{
    public class Runner : IDisposable
    {
        private readonly CompositeDisposable _compositeDisposable = new CompositeDisposable();
        
        private readonly ILogger _logger;
        private readonly ISchedulerProvider _schedulerProvider;
        private readonly ICloudService _cloudService;
        private readonly IDeviceService _deviceService;
        private readonly IClockService _clockService;
        private readonly IDeviceFirmwareUpdater _deviceFirmwareUpdater;

        private IDisposable _timedUpdateDisposable;
        private TimeSpan _currentTimerInterval = TimeSpan.Zero;
        private LightSchedule _currentLightSchedule = new LightSchedule();
        private readonly Dictionary<byte, Relay> _currentRelayStatus = new Dictionary<byte, Relay>();
        
        public Runner(ILogger logger, ISchedulerProvider schedulerProvider, ICloudService cloudService,
            IDeviceService deviceService, IClockService clockService, IDeviceFirmwareUpdater deviceFirmwareUpdater)
        {
            _logger = logger;
            _schedulerProvider = schedulerProvider;
            _cloudService = cloudService;
            _deviceService = deviceService;
            _clockService = clockService;
            _deviceFirmwareUpdater = deviceFirmwareUpdater;
        }

        public void Run()
        {
            _logger.LogInformation("Starting runner");
            
            ObserveSetFan();
            ObserveLightSchedule();
            ObserveDeviceFirmwareUpdates();

            var connectedObservable = ConnectToSerialAndCloud();
            SendStatusAndHearForPeriodUpdates(connectedObservable);
        }

        private void ObserveSetFan()
        {
            var setFanObservable = _cloudService.SetFan()
                .SelectMany(fan => _deviceService.SetFan(fan))
                .RetryWhen(observable =>
                    observable
                        .Do(ex => _logger.LogWarning($"Setfan command throw {ex} subscribing again"))
                        .Zip(Observable.Return(0).Delay(TimeSpan.FromSeconds(30), _schedulerProvider.NewThread),
                            (exception, i) => i)
                        .Do(_ => _logger.LogInformation("Setfan command subscribing again"))
                )
                .SubscribeOn(_schedulerProvider.NewThread)
                .Subscribe(fanSetSuccess => _logger.LogInformation($"Set fan to new value successfully: {fanSetSuccess}"),
                    err => _logger.LogError($"Got error while setting fan to new value: {err}"),
                    () => _logger.LogInformation("Listing for set fan commands finished"));
            _compositeDisposable.Add(setFanObservable);
        }
        
        private void ObserveLightSchedule()
        {
            var setLightScheduleObservable = _cloudService.GetDesiredProperties()
                .Where(properties => properties?.LightSchedule != null)
                .Do(properties => _currentLightSchedule = properties.LightSchedule)
                .Do(_ => _logger.LogDebug($"Updated light schedule: {_currentLightSchedule}"))
                .Select(properties => (properties.LightSchedule, _clockService.HourOfDay()))
                .Merge(_clockService.Tick.Select(hourOfDay => (_currentLightSchedule, hourOfDay)))
                .Select(data => RelayStatusFromHourOfDay(data.Item1, data.Item2))
                .Where(relay =>
                    !_currentRelayStatus.ContainsKey(relay.Number) || !_currentRelayStatus[relay.Number].Equals(relay))
                .Do(relay => _currentRelayStatus[relay.Number] = relay)
                .Do(relay => _logger.LogInformation($"Update relay {relay.Number} to On={relay.On}"))
                .SelectMany(relay => _deviceService.SetRelay(relay))
                .SubscribeOn(_schedulerProvider.NewThread)
                .Subscribe(status => { _logger.LogInformation($"Updated relay success: {status}"); },
                    err => { _logger.LogError($"Device setRelay got error: {err}"); },
                    () => { _logger.LogInformation("Device setRelay finished"); });
            _compositeDisposable.Add(setLightScheduleObservable);
        }

        private static Relay RelayStatusFromHourOfDay(LightSchedule lightSchedule, double hourOfDay)
        {
            if (lightSchedule.On <= lightSchedule.Off)
            {
                var relayStatus = hourOfDay >= lightSchedule.On && hourOfDay < lightSchedule.Off;
                return new Relay {Number = 0, On = relayStatus};
            }
            else
            {
                var relayStatus = hourOfDay >= lightSchedule.On || hourOfDay < lightSchedule.Off;
                return new Relay {Number = 0, On = relayStatus};
            }
        }
        
        private void ObserveDeviceFirmwareUpdates()
        {
            var currentFirmware = new CurrentFirmware();
            _deviceFirmwareUpdater.UpdateFirmware
                .Select(firmware =>
                {
                    UpdateNotNullProperties(firmware, currentFirmware);
                    return currentFirmware;
                })
                .Subscribe(firmware =>
                    {
                        _logger.LogInformation(
                            $"Updated device firmware status to {firmware.FwUpdateStatus}, beacause {firmware.FwUpdateSubstatus}.\n" +
                            $"Version: {firmware.CurrentFwVersion} Pending: {firmware.PendingFwVersion}\n" +
                            $"Last update from {firmware.LastFwUpdateStartTime} until {firmware.LastFwUpdateEndTime}");
                    },
                    err => { _logger.LogError($"Device firmware throw {err}."); },
                    () => { _logger.LogInformation("Stop listing for device firmware updates."); });
        }

        private static void UpdateNotNullProperties(CurrentFirmware from, CurrentFirmware to)
        {
            to.FwUpdateStatus = from.FwUpdateStatus;
            if (!string.IsNullOrEmpty(from.FwUpdateSubstatus))
                to.FwUpdateSubstatus = from.FwUpdateSubstatus;
            if (!string.IsNullOrEmpty(from.CurrentFwVersion))
                to.CurrentFwVersion = from.CurrentFwVersion;
            if (!string.IsNullOrEmpty(from.PendingFwVersion))
                to.PendingFwVersion = from.PendingFwVersion;
            if (!string.IsNullOrEmpty(from.LastFwUpdateStartTime))
                to.LastFwUpdateStartTime = from.LastFwUpdateStartTime;
            if (!string.IsNullOrEmpty(from.LastFwUpdateEndTime))
                to.LastFwUpdateEndTime = from.LastFwUpdateEndTime;
        }

        private IObservable<Unit> ConnectToSerialAndCloud()
        {
            var connectedObservable = Observable.FromAsync(async () => await _deviceService.Connect())
                .SelectMany(_ => Observable.FromAsync(async () => await _cloudService.Connect()));
            return connectedObservable;
        }

        private void SendStatusAndHearForPeriodUpdates(IObservable<Unit> connectedObservable)
        {
            // Get TelemetryConfig.Period from reported properties to update StartIntervalledStatusUpdate with new TimeSpan
            var newIntervalObservable = _cloudService.GetDesiredProperties()
                .Where(properties => properties?.TelemetryConfig != null)
                .Select(properties => properties.TelemetryConfig.Period)
                .Select(TimeSpan.FromSeconds)
                // When device and cloud are connected, start once with default interval, in case reported properties
                // doesn't contain TelemetryConfig.Period or they aren't reported yet
                .Merge(connectedObservable.Select(_ => TimeSpan.FromMinutes(15)))
                .Where(timespan => timespan != _currentTimerInterval)
                .Do(timespan => _currentTimerInterval = timespan)
                .SubscribeOn(_schedulerProvider.NewThread)
                .Subscribe(timespan =>
                    {
                        _logger.LogInformation($"Setup new interval for device status. Interval is {timespan.TotalMinutes} minutes");
                        StartIntervalledStatusUpdate(timespan);
                    },
                    err => { _logger.LogError($"Got error while listining for TelemetryConfig.Period: {err}"); },
                    () => { _logger.LogInformation("Listing for TelemetryConfig.Period finished"); });

            _compositeDisposable.Add(newIntervalObservable);
        }

        private void StartIntervalledStatusUpdate(TimeSpan timespan)
        {
            _timedUpdateDisposable?.Dispose();
            _timedUpdateDisposable = Observable.Interval(timespan, _schedulerProvider.NewThread)
                .Merge(Observable.Return(0L))
                .SelectMany(_ => _deviceService.GetStatus())
                .SelectMany(SendStatusToCloudWithRetry)
                .SubscribeOn(_schedulerProvider.NewThread)
                .Subscribe(status => { _logger.LogInformation($"Updated status: {status}"); },
                    err => { _logger.LogError($"Device status interval got error: {err}"); },
                    () => { _logger.LogInformation("Device status interval finished"); });
        }

        private IObservable<DeviceStatus> SendStatusToCloudWithRetry(DeviceStatus status)
        {
            return Observable.FromAsync(() => _cloudService.SendStatusMessage(status))
                .Select(_ => status)
                .RetryWhen(observable =>
                    observable
                        .Do(ex => _logger.LogWarning($"SendStatusMessage throw {ex} trying again"))
                        .Zip(Observable.Return(0).Delay(TimeSpan.FromSeconds(30), _schedulerProvider.NewThread),
                        (exception, i) => i)
                        .Do(_ => _logger.LogInformation("SendStatusMessage trying again"))
                    );
        }

        public void Dispose()
        {
            _logger.LogInformation("Disposing runner");
            _timedUpdateDisposable?.Dispose();
            _compositeDisposable?.Dispose();
        }
    }
}