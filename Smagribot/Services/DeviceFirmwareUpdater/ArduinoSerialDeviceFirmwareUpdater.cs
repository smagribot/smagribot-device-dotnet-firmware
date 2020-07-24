using System;
using System.Globalization;
using System.IO;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Smagribot.Models.DeviceProperties;
using Smagribot.Services.Cloud;
using Smagribot.Services.Device;
using Smagribot.Services.DeviceFirmwareUpdater.Exceptions;
using Smagribot.Services.Utils;

namespace Smagribot.Services.DeviceFirmwareUpdater
{
    public class ArduinoSerialDeviceFirmwareUpdater : IDeviceFirmwareUpdater
    {
        private readonly string _firmwareDownloadPath;
        private readonly string _serialPortName;
        private readonly string _fqbn;

        private readonly ILogger _logger;
        private readonly ICloudService _cloudService;
        private readonly IDeviceService _deviceService;
        private readonly IHttpClient _httpClient;
        private readonly IChecksum _checksum;
        private readonly IArduinoCli _arduinoCli;

        private bool _isUpdatingFirmware = false;

        public ArduinoSerialDeviceFirmwareUpdater(string firmwareDownloadPath,
            string serialPortName,
            string fqbn,
            ILogger logger,
            ICloudService cloudService,
            IDeviceService deviceService,
            IHttpClient httpClient,
            IChecksum checksum,
            IArduinoCli arduinoCli)
        {
            _firmwareDownloadPath = firmwareDownloadPath;
            _serialPortName = serialPortName;
            _fqbn = fqbn;
            _logger = logger;
            _cloudService = cloudService;
            _deviceService = deviceService;
            _httpClient = httpClient;
            _checksum = checksum;
            _arduinoCli = arduinoCli;

            var desiredFirmware = ListenForDesiredFirmware();

            UpdateFirmware =
                ActionOnFirmwarePropertyChanges(desiredFirmware);
        }

        private IObservable<(ArduinoSerialFirmware desiredFw, Version desiredFwVersion, Version deviceFwVersion)> ListenForDesiredFirmware()
        {
            return _cloudService.GetDesiredProperties()
                .Where(props => props?.ArduinoSerialFirmware != null)
                .Select(props => props.ArduinoSerialFirmware)
                .SelectMany(async desiredFw =>
                {
                    var deviceFwVersion = await _deviceService.GetFirmware();
                    var desiredFwVersion = Version.Parse(desiredFw.FwVersion);
                    return (desiredFw, desiredFwVersion, deviceFwVersion);
                })
                .Publish()
                .RefCount();
        }

        private IObservable<CurrentFirmware> ActionOnFirmwarePropertyChanges(IObservable<(ArduinoSerialFirmware desiredFw, Version desiredFwVersion, Version deviceFwVersion)> desiredFirmware)
        {
            return FirmwareIsCurrent(desiredFirmware)
                .Merge(FirmwareIsNotCurrent(desiredFirmware))
                .Select(currentArudinoSerialFw => new ReportedDeviceProperties
                    {Firmware = currentArudinoSerialFw})
                .SelectMany(properties => Observable.FromAsync(() => _cloudService.UpdateProperties(properties))
                    .Select(_ => properties.Firmware))
                .Publish()
                .RefCount();
        }

        public IObservable<CurrentFirmware> UpdateFirmware { get; }
        
        private static IObservable<CurrentFirmware> FirmwareIsCurrent(
            IObservable<(ArduinoSerialFirmware, Version, Version)> data)
        {
            return data
                .Where(fws => fws.Item2 == fws.Item3)
                .Select(fws => new CurrentFirmware
                {
                    CurrentFwVersion = fws.Item3.ToString(),
                    FwUpdateStatus = UpdateStatus.Current,
                    FwUpdateSubstatus = "Firmware already up to date"

                });
        }
        
        private IObservable<CurrentFirmware> FirmwareIsNotCurrent(
            IObservable<(ArduinoSerialFirmware, Version, Version)> data)
        {
            return data
                .Where(fws => fws.Item2 != fws.Item3)
                .SelectMany(fws => FirmwareUpdate(fws.Item1, fws.Item3));
        }

        private IObservable<CurrentFirmware> FirmwareUpdate(ArduinoSerialFirmware fw, Version deviceVersion)
        {
            return Observable.Create<CurrentFirmware>(async observer =>
            {
                if (_isUpdatingFirmware)
                {
                    observer.OnNext(new CurrentFirmware
                    {
                        FwUpdateStatus = UpdateStatus.Error,
                        FwUpdateSubstatus = "Firmware update in progress"
                    });
                    
                    observer.OnCompleted();
                    return Disposable.Empty;
                }
                
                _isUpdatingFirmware = true;
                
                observer.OnNext(new CurrentFirmware
                {
                    CurrentFwVersion = deviceVersion.ToString(),
                    PendingFwVersion = fw.FwVersion,
                    LastFwUpdateStartTime = DateTime.Now.ToString(CultureInfo.InvariantCulture),
                    FwUpdateStatus = UpdateStatus.Downloading,
                    FwUpdateSubstatus = "Start downloading"
                });

                try
                {
                    var dlpath = await DownloadFirmware(fw);

                    observer.OnNext(new CurrentFirmware
                    {
                        FwUpdateStatus = UpdateStatus.Verifying,
                        FwUpdateSubstatus = "Start verifying download"
                    });

                    await CheckDownloadedFirmware(fw, dlpath);

                    observer.OnNext(new CurrentFirmware
                    {
                        FwUpdateStatus = UpdateStatus.Applying,
                        FwUpdateSubstatus = "Applying image"
                    });
                    
                    await ApplyFirmware(dlpath);

                    observer.OnNext(new CurrentFirmware
                    {
                        FwUpdateStatus = UpdateStatus.Rebooting,
                        FwUpdateSubstatus = "Rebooting connection"
                    });
                    
                    await _deviceService.Connect();
                    
                    observer.OnNext(new CurrentFirmware
                    {
                        CurrentFwVersion = fw.FwVersion,
                        PendingFwVersion = "",
                        LastFwUpdateEndTime = DateTime.Now.ToString(CultureInfo.InvariantCulture),
                        FwUpdateStatus = UpdateStatus.Current,
                        FwUpdateSubstatus = "Update successfull"
                    });
                }
                catch (Exception e)
                {
                    _logger.LogError($"Couldn't update firmware: {e.Message}");
                    _logger.LogDebug($"Couldn't update firmware: {e}");
                    observer.OnNext(new CurrentFirmware
                        {
                            FwUpdateStatus = UpdateStatus.Error,
                            FwUpdateSubstatus = $"Couldn't update firmware: {e.Message}"
                        }
                    );
                }
                finally
                {
                    _isUpdatingFirmware = false;
                }
                
                observer.OnCompleted();
                
                return Disposable.Empty;
            });
        }

        private async Task<string> DownloadFirmware(ArduinoSerialFirmware fw)
        {
            var dlPath = Path.Combine(_firmwareDownloadPath, $"arduinoserial_{fw.FwVersion}.hex");
            await _httpClient.SafeFile(fw.FwPackageUri, dlPath);
            return dlPath;
        }

        private async Task CheckDownloadedFirmware(ArduinoSerialFirmware fw, string dlpath)
        {
            var fileCheck = await _checksum.Md5(dlpath);
            if (fileCheck != fw.FwPackageCheckValue)
                throw new ChecksumException("Checksum is invalid try again!");
        }
        
        private async Task ApplyFirmware(string firmwarePath)
        {
            await _deviceService.Disconnect();

            try
            {
                await _arduinoCli.Upload(firmwarePath, _serialPortName, _fqbn);
            }
            catch
            {
                await _deviceService.Connect();
                throw;
            }
        }
    }
}
