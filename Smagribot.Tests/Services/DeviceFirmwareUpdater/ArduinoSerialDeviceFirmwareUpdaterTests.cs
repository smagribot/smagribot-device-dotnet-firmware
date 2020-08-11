using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Smagribot.Models.DeviceProperties;
using Smagribot.Services.Cloud;
using Smagribot.Services.Device;
using Smagribot.Services.DeviceFirmwareUpdater;
using Smagribot.Services.Utils;
using Smagribot.Tests.TestHelpers;
using Xunit;

namespace Smagribot.Tests.Services.DeviceFirmwareUpdater
{
    public class ArduinoSerialDeviceFirmwareUpdaterTests
    {
        private readonly ArduinoSerialDeviceFirmwareUpdater _sut;
        
        private const string FwDownloadPath = "/path/to/firmwaredl";
        private const string SerialPortName = "/dev/ttyACM0";
        private const string Fqbn = "arduino:avr:uno";
        
        // private readonly MockSequence _mockSequence = new MockSequence();
        
        private readonly TestSchedulerProvider _schedulerProvider = new TestSchedulerProvider();
        private readonly Mock<ICloudService> _cloudService = new Mock<ICloudService>();
        private readonly Mock<IDeviceService> _deviceService = new Mock<IDeviceService>();
        private readonly Mock<IHttpClient> _httpClient = new Mock<IHttpClient>();
        private readonly Mock<IChecksum> _checksum = new Mock<IChecksum>();
        private readonly Mock<IArduinoCli> _arduinoCli = new Mock<IArduinoCli>();

        private readonly Subject<DesiredDeviceProperties> _desiredPropertiesSubj = new Subject<DesiredDeviceProperties>();

        private readonly DesiredDeviceProperties _002Firmware = new DesiredDeviceProperties
        {
            ArduinoSerialFirmware = new ArduinoSerialFirmware
            {
                FwVersion = "0.0.2",
                FwPackageUri = "https://firmware.com:8080/file_0.0.2.hex?token=abc",
                FwPackageCheckValue = "abc123"
            }
        };

        protected ArduinoSerialDeviceFirmwareUpdaterTests()
        {
            _cloudService.Setup(m => m.GetDesiredProperties()).Returns(_desiredPropertiesSubj);
            _deviceService.Setup(m => m.GetFirmware())
                .ReturnsAsync(Version.Parse("0.0.1"));
            _checksum.Setup(m => m.Md5("/path/to/firmwaredl/arduinoserial_0.0.2.hex"))
                .ReturnsAsync("abc123");

            _sut = new ArduinoSerialDeviceFirmwareUpdater(
                FwDownloadPath,
                SerialPortName,
                Fqbn,
                new Mock<ILogger>().Object,
                _cloudService.Object,
                _deviceService.Object,
                _httpClient.Object,
                _checksum.Object,
                _arduinoCli.Object
            );
        }

        // private void SetupCloudServiceSequence()
        // {
        //     //Downloading
        //     _cloudService.InSequence(_mockSequence).Setup(m => 
        //         m.UpdateProperties(It.Is<ReportedDeviceProperties>(props => 
        //             props.ArduinoSerialFirmware.FwUpdateStatus == UpdateStatus.Downloading
        //             && !string.IsNullOrEmpty(props.ArduinoSerialFirmware.FwUpdateSubstatus)
        //         ))
        //     ).Returns(Task.CompletedTask);
        //         
        //     //Verifying
        //     _cloudService.InSequence(_mockSequence).Setup(m => 
        //         m.UpdateProperties(It.Is<ReportedDeviceProperties>(props => 
        //             props.ArduinoSerialFirmware.FwUpdateStatus == UpdateStatus.Verifying
        //             && !string.IsNullOrEmpty(props.ArduinoSerialFirmware.FwUpdateSubstatus)
        //         ))
        //     ).Returns(Task.CompletedTask);
        //         
        //     //Applying
        //     _cloudService.InSequence(_mockSequence).Setup(m => 
        //         m.UpdateProperties(It.Is<ReportedDeviceProperties>(props => 
        //             props.ArduinoSerialFirmware.FwUpdateStatus == UpdateStatus.Applying
        //             && !string.IsNullOrEmpty(props.ArduinoSerialFirmware.FwUpdateSubstatus)
        //         ))
        //     ).Returns(Task.CompletedTask);
        //         
        //     //Rebooting
        //     _cloudService.InSequence(_mockSequence).Setup(m => 
        //         m.UpdateProperties(It.Is<ReportedDeviceProperties>(props => 
        //             props.ArduinoSerialFirmware.FwUpdateStatus == UpdateStatus.Rebooting
        //             && !string.IsNullOrEmpty(props.ArduinoSerialFirmware.FwUpdateSubstatus)
        //         ))
        //     ).Returns(Task.CompletedTask);
        //     
        //     //Error
        //     _cloudService.InSequence(_mockSequence).Setup(m => 
        //         m.UpdateProperties(It.Is<ReportedDeviceProperties>(props => 
        //             props.ArduinoSerialFirmware.FwUpdateStatus == UpdateStatus.Error
        //             && !string.IsNullOrEmpty(props.ArduinoSerialFirmware.FwUpdateSubstatus)
        //         ))
        //     ).Returns(Task.CompletedTask);
        // }

        private void VerifyUpdateProperties(UpdateStatus updateStatus, Times times)
        {
            _cloudService.Verify(m => 
                m.UpdateProperties(It.Is<ReportedDeviceProperties>(props => 
                    props.Firmware.FwUpdateStatus == updateStatus
                    && !string.IsNullOrEmpty(props.Firmware.FwUpdateSubstatus)
                )), times
            );
        }

        public class UpdateFirmware : ArduinoSerialDeviceFirmwareUpdaterTests
        {
            [Fact(Skip = "Throws: Unsupported expression: m => m.Subscribe<DesiredDeviceProperties>(It.IsAny<Action<DesiredDeviceProperties>>(), It.IsAny<Action<Exception>>(), It.IsAny<Action>()) " +
                         "Extension methods (here: ObservableExtensions.Subscribe) may not be used in setup / verification expressions.")]
            public void Should_subscribe_GetDesiredProperties()
            {
                var mockObservable = new Mock<IObservable<DesiredDeviceProperties>>();
                _cloudService.Setup(m => m.GetDesiredProperties()).Returns(mockObservable.Object);

                _sut.UpdateFirmware.Subscribe();

                    mockObservable.Verify(m => m.Subscribe(
                        It.IsAny<Action<DesiredDeviceProperties>>(),
                        It.IsAny<Action<Exception>>(), 
                        It.IsAny<Action>()));
            }

            [Fact]
            public void Should_send_already_up_to_date_status()
            {
                _deviceService.Setup(m => m.GetFirmware())
                    .ReturnsAsync(Version.Parse("0.0.2"));
                
                _sut.UpdateFirmware.Subscribe();
                
                _desiredPropertiesSubj.OnNext(_002Firmware);
                
                VerifyUpdateProperties(UpdateStatus.Current, Times.Once());
                VerifyUpdateProperties(UpdateStatus.Error, Times.Never());
                VerifyUpdateProperties(UpdateStatus.Downloading, Times.Never());
                VerifyUpdateProperties(UpdateStatus.Verifying, Times.Never());
                VerifyUpdateProperties(UpdateStatus.Applying, Times.Never());
                VerifyUpdateProperties(UpdateStatus.Rebooting, Times.Never());
            }
            
            [Fact]
            public void Should_send_correct_status_in_order()
            {
                _sut.UpdateFirmware.Subscribe();
                
                _desiredPropertiesSubj.OnNext(_002Firmware);
                
                VerifyUpdateProperties(UpdateStatus.Downloading, Times.Once());
                VerifyUpdateProperties(UpdateStatus.Verifying, Times.Once());
                VerifyUpdateProperties(UpdateStatus.Applying, Times.Once());
                VerifyUpdateProperties(UpdateStatus.Rebooting, Times.Once());
                VerifyUpdateProperties(UpdateStatus.Current, Times.Once());
                VerifyUpdateProperties(UpdateStatus.Error, Times.Never());
                
                _desiredPropertiesSubj.OnNext(_002Firmware);
                
                VerifyUpdateProperties(UpdateStatus.Downloading, Times.Exactly(2));
                VerifyUpdateProperties(UpdateStatus.Verifying, Times.Exactly(2));
                VerifyUpdateProperties(UpdateStatus.Applying, Times.Exactly(2));
                VerifyUpdateProperties(UpdateStatus.Rebooting, Times.Exactly(2));
                VerifyUpdateProperties(UpdateStatus.Current, Times.Exactly(2));
                VerifyUpdateProperties(UpdateStatus.Error, Times.Never());
            }
            
            [Fact]
            public void Should_send_error_when_update_is_already_running()
            {
                var taskCmpltnSrc = new TaskCompletionSource<bool>();
                _httpClient.Setup(m => m.SafeFile(It.IsAny<string>(), It.IsAny<string>()))
                    .Returns(taskCmpltnSrc.Task);
                
                _sut.UpdateFirmware.SubscribeOn(_schedulerProvider).Subscribe();
                _schedulerProvider.AdvanceBy(1);
                
                _desiredPropertiesSubj.OnNext(_002Firmware);
                _desiredPropertiesSubj.OnNext(_002Firmware);
                
                VerifyUpdateProperties(UpdateStatus.Downloading, Times.Once());
                VerifyUpdateProperties(UpdateStatus.Error, Times.Once());
                
                VerifyUpdateProperties(UpdateStatus.Verifying, Times.Never());
                VerifyUpdateProperties(UpdateStatus.Applying, Times.Never());
                VerifyUpdateProperties(UpdateStatus.Rebooting, Times.Never());
                VerifyUpdateProperties(UpdateStatus.Current, Times.Never());
            }
            
            [Fact]
            public void Should_start_downloading_fw()
            {
                _sut.UpdateFirmware.Subscribe();
                
                _desiredPropertiesSubj.OnNext(_002Firmware);
                
                _httpClient.Verify(m => m.SafeFile("https://firmware.com:8080/file_0.0.2.hex?token=abc", "/path/to/firmwaredl/arduinoserial_0.0.2.hex"));
            }
            
            [Fact]
            public void Should_send_error_when_downlading_fw_throws()
            {
                _httpClient.Setup(m => m.SafeFile(It.IsAny<string>(), It.IsAny<string>()))
                    .ThrowsAsync(new Exception());
                
                _sut.UpdateFirmware.Subscribe();
                
                _desiredPropertiesSubj.OnNext(_002Firmware);
                
                VerifyUpdateProperties(UpdateStatus.Downloading, Times.Once());
                VerifyUpdateProperties(UpdateStatus.Error, Times.Once());
                VerifyUpdateProperties(UpdateStatus.Verifying, Times.Never());
                VerifyUpdateProperties(UpdateStatus.Applying, Times.Never());
                VerifyUpdateProperties(UpdateStatus.Rebooting, Times.Never());
                VerifyUpdateProperties(UpdateStatus.Current, Times.Never());
            }

            [Fact]
            public void Should_checksum_md5_downloaded_file()
            {
                _sut.UpdateFirmware.Subscribe();
                
                _desiredPropertiesSubj.OnNext(_002Firmware);
                
                _checksum.Verify(m => m.Md5("/path/to/firmwaredl/arduinoserial_0.0.2.hex"));   
            }
            
            [Fact]
            public void Should_send_error_when_checksum_md5_throws()
            {
                _checksum.Setup(m => m.Md5(It.IsAny<string>()))
                    .ThrowsAsync(new Exception());
                
                _sut.UpdateFirmware.Subscribe();
                
                _desiredPropertiesSubj.OnNext(_002Firmware);
                
                VerifyUpdateProperties(UpdateStatus.Downloading, Times.Once());
                VerifyUpdateProperties(UpdateStatus.Verifying, Times.Once());
                VerifyUpdateProperties(UpdateStatus.Error, Times.Once());
                VerifyUpdateProperties(UpdateStatus.Applying, Times.Never());
                VerifyUpdateProperties(UpdateStatus.Rebooting, Times.Never());
                VerifyUpdateProperties(UpdateStatus.Current, Times.Never());
            }
            
            [Fact]
            public void Should_send_error_when_checksum_isnt_same()
            {
                _checksum.Setup(m => m.Md5(It.IsAny<string>()))
                    .ReturnsAsync("not_the_expected_checksum");
                
                _sut.UpdateFirmware.Subscribe();
                
                _desiredPropertiesSubj.OnNext(_002Firmware);
                
                VerifyUpdateProperties(UpdateStatus.Downloading, Times.Once());
                VerifyUpdateProperties(UpdateStatus.Verifying, Times.Once());
                VerifyUpdateProperties(UpdateStatus.Error, Times.Once());
                VerifyUpdateProperties(UpdateStatus.Applying, Times.Never());
                VerifyUpdateProperties(UpdateStatus.Rebooting, Times.Never());
                VerifyUpdateProperties(UpdateStatus.Current, Times.Never());
            }

            [Fact]
            public void Should_apply_firmware_should_disconnect_then_upload_fw_device_and_then_connect()
            {
                var deviceService = new Mock<IDeviceService>(MockBehavior.Strict);
                var arduinoCli = new Mock<IArduinoCli>(MockBehavior.Strict);
                var sequence = new MockSequence();
                
                deviceService.Setup(m => m.GetFirmware())
                    .ReturnsAsync(Version.Parse("0.0.1"));
                deviceService.InSequence(sequence)
                    .Setup(m => m.Disconnect())
                    .Returns(Task.CompletedTask);
                arduinoCli.InSequence(sequence)
                    .Setup(m => m.Upload("/path/to/firmwaredl/arduinoserial_0.0.2.hex", SerialPortName, Fqbn))
                    .Returns(Task.CompletedTask);
                deviceService.InSequence(sequence)
                    .Setup(m => m.Connect())
                    .Returns(Task.CompletedTask);

                var sut = new ArduinoSerialDeviceFirmwareUpdater(FwDownloadPath, SerialPortName, Fqbn,
                    new Mock<ILogger>().Object, _cloudService.Object, deviceService.Object, _httpClient.Object,
                    _checksum.Object, arduinoCli.Object);
                
                sut.UpdateFirmware.Subscribe();
                
                _desiredPropertiesSubj.OnNext(_002Firmware);
                
                deviceService.Verify(m => m.Disconnect());
                arduinoCli.Verify(m => m.Upload("/path/to/firmwaredl/arduinoserial_0.0.2.hex", SerialPortName, Fqbn));
                deviceService.Verify(m => m.Connect());
            }
            
            [Fact]
            public void Should_send_error_when_cant_disconnect()
            {
                _deviceService.Setup(m => m.Disconnect())
                    .ThrowsAsync(new Exception());
                
                _sut.UpdateFirmware.Subscribe();
                
                _desiredPropertiesSubj.OnNext(_002Firmware);
                
                VerifyUpdateProperties(UpdateStatus.Downloading, Times.Once());
                VerifyUpdateProperties(UpdateStatus.Verifying, Times.Once());
                VerifyUpdateProperties(UpdateStatus.Applying, Times.Once());
                VerifyUpdateProperties(UpdateStatus.Error, Times.Once());
                VerifyUpdateProperties(UpdateStatus.Rebooting, Times.Never());
                VerifyUpdateProperties(UpdateStatus.Current, Times.Never());
            }
            
            [Fact]
            public void Should_send_error_when_cant_upload_firmware()
            {
                _arduinoCli.Setup(m => m.Upload(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                    .ThrowsAsync(new Exception());
                
                _sut.UpdateFirmware.Subscribe();
                
                _desiredPropertiesSubj.OnNext(_002Firmware);
                
                VerifyUpdateProperties(UpdateStatus.Downloading, Times.Once());
                VerifyUpdateProperties(UpdateStatus.Verifying, Times.Once());
                VerifyUpdateProperties(UpdateStatus.Applying, Times.Once());
                VerifyUpdateProperties(UpdateStatus.Error, Times.Once());
                VerifyUpdateProperties(UpdateStatus.Rebooting, Times.Never());
                VerifyUpdateProperties(UpdateStatus.Current, Times.Never());
            }
            
            [Fact]
            public void Should_reconnect_when_cant_upload_firmware()
            {
                _arduinoCli.Setup(m => m.Upload(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                    .ThrowsAsync(new Exception());
                
                _sut.UpdateFirmware.Subscribe();
                
                _desiredPropertiesSubj.OnNext(_002Firmware);
                
                _deviceService.Verify(m => m.Connect());
            }
            
            [Fact]
            public void Should_send_error_when_cant_connect()
            {
                _deviceService.Setup(m => m.Connect())
                    .ThrowsAsync(new Exception());
                
                _sut.UpdateFirmware.Subscribe();
                
                _desiredPropertiesSubj.OnNext(_002Firmware);
                
                VerifyUpdateProperties(UpdateStatus.Downloading, Times.Once());
                VerifyUpdateProperties(UpdateStatus.Verifying, Times.Once());
                VerifyUpdateProperties(UpdateStatus.Applying, Times.Once());
                VerifyUpdateProperties(UpdateStatus.Error, Times.Once());
                VerifyUpdateProperties(UpdateStatus.Rebooting, Times.Once());
                VerifyUpdateProperties(UpdateStatus.Current, Times.Never());
            }
        }
    }
}