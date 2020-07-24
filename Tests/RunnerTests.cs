using System;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Smagribot;
using Smagribot.Models.DeviceProperties;
using Smagribot.Models.Messages;
using Smagribot.Models.Methods;
using Smagribot.Services.Cloud;
using Smagribot.Services.Device;
using Smagribot.Services.DeviceFirmwareUpdater;
using Smagribot.Services.Utils;
using Tests.TestHelpers;
using Xunit;

namespace Tests
{
    public class RunnerTests
    {
        private readonly Runner _sut;

        private readonly TestSchedulerProvider _schedulerProvider = new TestSchedulerProvider();
        private readonly Mock<ILogger> _loggerMock = new Mock<ILogger>();
        private readonly Mock<ICloudService> _cloudServiceMock = new Mock<ICloudService>();
        private readonly Mock<IDeviceService> _deviceServiceMock = new Mock<IDeviceService>();
        private readonly Mock<IClockService> _clockServiceMock = new Mock<IClockService>();
        private readonly Mock<IDeviceFirmwareUpdater> _deviceFirmwareUpdaterMock = new Mock<IDeviceFirmwareUpdater>();
        
        private readonly Subject<DesiredDeviceProperties> _devicePropertiesSubject = new Subject<DesiredDeviceProperties>();
        private readonly Subject<Fan> _fanSubject = new Subject<Fan>();
        private readonly Subject<double> _clockTickSubject = new Subject<double>();
        private readonly Subject<CurrentFirmware> _firmware = new Subject<CurrentFirmware>();
        

        protected RunnerTests()
        {
            _cloudServiceMock.Setup(m => m.GetDesiredProperties())
                .Returns(_devicePropertiesSubject);
            _cloudServiceMock.Setup(m => m.SetFan())
                .Returns(_fanSubject);
            _clockServiceMock.SetupGet(m => m.Tick).Returns(_clockTickSubject);

            _deviceFirmwareUpdaterMock.SetupGet(m => m.UpdateFirmware)
                .Returns(_firmware);

            _sut = new Runner(_loggerMock.Object,
                _schedulerProvider,
                _cloudServiceMock.Object,
                _deviceServiceMock.Object,
                _clockServiceMock.Object,
                _deviceFirmwareUpdaterMock.Object);
        }

        public class Run : RunnerTests
        {
            [Fact]
            public void Should_connect_to_device()
            {
                _sut.Run();

                _schedulerProvider.AdvanceTo(1);
                _deviceServiceMock.Verify(m => m.Connect());
            }
            
            [Fact]
            public void Should_connect_to_cloud()
            {
                _sut.Run();

                _schedulerProvider.AdvanceTo(1);
                _cloudServiceMock.Verify(m => m.Connect());
            }

            [Fact]
            public void Should_not_connect_to_cloud_when_cant_connect_to_device()
            {
                _deviceServiceMock.Setup(m => m.Connect()).ThrowsAsync(new Exception());
                
                _sut.Run();

                _schedulerProvider.AdvanceTo(1);
                _deviceServiceMock.Verify(m => m.Connect());
                _cloudServiceMock.Verify(m => m.Connect(), Times.Never);
            }

            [Fact]
            public void Should_get_every_15_minutes_device_status_starting_at_0_by_default()
            {
                _sut.Run();
                
                _schedulerProvider.AdvanceTo(2);
                _deviceServiceMock.Verify(m => m.GetStatus(), Times.Exactly(1));
                
                _schedulerProvider.AdvanceBy(TimeSpan.FromMinutes(15).Ticks);
                _deviceServiceMock.Verify(m => m.GetStatus(), Times.Exactly(2));
                
                _schedulerProvider.AdvanceBy(TimeSpan.FromMinutes(15).Ticks);
                _deviceServiceMock.Verify(m => m.GetStatus(), Times.Exactly(3));
            }
            
            [Fact]
            public void Should_not_get_device_status_when_device_isnt_connected()
            {
                var deviceConnectTaskCompliationSource = new TaskCompletionSource<bool>();
                _deviceServiceMock.Setup(m => m.Connect()).Returns(() => deviceConnectTaskCompliationSource.Task);
                
                _sut.Run();
                
                _schedulerProvider.AdvanceTo(2);
                _deviceServiceMock.Verify(m => m.GetStatus(), Times.Never);
            }
            
            [Fact]
            public void Should_not_get_device_status_when_device_cant_connect_to_device()
            {
                _deviceServiceMock.Setup(m => m.Connect()).ThrowsAsync(new Exception());
                
                _sut.Run();
                
                _schedulerProvider.AdvanceTo(2);
                _deviceServiceMock.Verify(m => m.GetStatus(), Times.Never);
            }

            [Fact]
            public void Should_send_status_to_cloud()
            {
                var deviceStatus = new DeviceStatus();
                _deviceServiceMock.Setup(m => m.GetStatus())
                    .ReturnsAsync(deviceStatus);
                
                _sut.Run();
                
                _schedulerProvider.AdvanceTo(2);
                _cloudServiceMock.Verify(m => m.SendStatusMessage(deviceStatus), Times.Exactly(1));
            }
            
            [Fact]
            public void Should_not_send_status_to_cloud_when_status_cant_be_retrieved()
            {
                _deviceServiceMock.Setup(m => m.GetStatus())
                    .ThrowsAsync(new Exception());
                
                _sut.Run();
                
                _schedulerProvider.AdvanceTo(2);
                _cloudServiceMock.Verify(m => m.SendStatusMessage(It.IsAny<DeviceStatus>()), Times.Never);
            }

            [Fact]
            public void Should_retry_sending_status_cloud_when_it_fails_after_30_sec()
            {
                var deviceStatus = new DeviceStatus();
                _deviceServiceMock.Setup(m => m.GetStatus())
                    .ReturnsAsync(deviceStatus);

                _cloudServiceMock.Setup(m => m.SendStatusMessage(It.IsAny<DeviceStatus>()))
                    .ThrowsAsync(new Exception());

                _sut.Run();
                _schedulerProvider.AdvanceTo(2);
                _cloudServiceMock.Verify(m => m.SendStatusMessage(deviceStatus), Times.Exactly(1));
                
                _schedulerProvider.AdvanceBy(TimeSpan.FromSeconds(15).Ticks);
                _cloudServiceMock.Verify(m => m.SendStatusMessage(deviceStatus), Times.Exactly(1));
                
                _schedulerProvider.AdvanceBy(TimeSpan.FromSeconds(15).Ticks);
                _cloudServiceMock.Verify(m => m.SendStatusMessage(deviceStatus), Times.Exactly(2));
            }

            [Fact]
            public void Should_listen_for_reported_properties()
            {
                _sut.Run();
                
                _schedulerProvider.AdvanceTo(2);
                _cloudServiceMock.Verify(m => m.GetDesiredProperties());
            }
            
            [Fact]
            public void Should_update_device_status_timer_when_new_period_is_set()
            {
                var devPropertiesWithNewPeriod = new DesiredDeviceProperties
                {
                    TelemetryConfig = new TelemetryConfig
                    {
                        Period = TimeSpan.FromMinutes(5).TotalSeconds
                    }
                };
                
                _sut.Run();

                _schedulerProvider.AdvanceTo(2);
                _cloudServiceMock.Verify(m => m.SendStatusMessage(It.IsAny<DeviceStatus>()), Times.Exactly(1));

                _schedulerProvider.AdvanceBy(TimeSpan.FromMinutes(15).Ticks);
                _deviceServiceMock.Verify(m => m.GetStatus(), Times.Exactly(2));
                
                //Update to new time
                _devicePropertiesSubject.OnNext(devPropertiesWithNewPeriod);
                
                _schedulerProvider.AdvanceBy(1);
                _deviceServiceMock.Verify(m => m.GetStatus(), Times.Exactly(3));
                
                _schedulerProvider.AdvanceBy(TimeSpan.FromMinutes(5).Ticks);
                _deviceServiceMock.Verify(m => m.GetStatus(), Times.Exactly(4));
                
                _schedulerProvider.AdvanceBy(TimeSpan.FromMinutes(5).Ticks);
                _deviceServiceMock.Verify(m => m.GetStatus(), Times.Exactly(5));
                
                _schedulerProvider.AdvanceBy(TimeSpan.FromMinutes(5).Ticks);
                _deviceServiceMock.Verify(m => m.GetStatus(), Times.Exactly(6));
            }

            [Fact]
            public void Should_not_update_device_status_timer_when_it_doesnt_changed()
            {
                var devPropertiesWithNewPeriod = new DesiredDeviceProperties
                {
                    TelemetryConfig = new TelemetryConfig
                    {
                        Period = TimeSpan.FromMinutes(15).TotalSeconds
                    }
                };
                
                _sut.Run();

                _schedulerProvider.AdvanceTo(2);
                _cloudServiceMock.Verify(m => m.SendStatusMessage(It.IsAny<DeviceStatus>()), Times.Exactly(1));

                _schedulerProvider.AdvanceBy(TimeSpan.FromMinutes(15).Ticks);
                _deviceServiceMock.Verify(m => m.GetStatus(), Times.Exactly(2));
                
                //Update to new time
                _devicePropertiesSubject.OnNext(devPropertiesWithNewPeriod);
                
                _schedulerProvider.AdvanceBy(1);
                _deviceServiceMock.Verify(m => m.GetStatus(), Times.Exactly(2));
                
                _schedulerProvider.AdvanceBy(TimeSpan.FromMinutes(15).Ticks);
                _deviceServiceMock.Verify(m => m.GetStatus(), Times.Exactly(3));
            }
            
            [Fact]
            public void Should_only_listen_to_device_properties_with_TelemetryConfig()
            {
                var devPropertiesWithNewPeriod = new DesiredDeviceProperties
                {
                    TelemetryConfig = new TelemetryConfig
                    {
                        Period = TimeSpan.FromMinutes(5).TotalSeconds
                    }
                };
                
                _sut.Run();

                _schedulerProvider.AdvanceTo(2);
                _cloudServiceMock.Verify(m => m.SendStatusMessage(It.IsAny<DeviceStatus>()), Times.Exactly(1));

                _schedulerProvider.AdvanceBy(TimeSpan.FromMinutes(15).Ticks);
                _deviceServiceMock.Verify(m => m.GetStatus(), Times.Exactly(2));
                
                //Update without TelemetryConfig
                _devicePropertiesSubject.OnNext(new DesiredDeviceProperties());
                
                _schedulerProvider.AdvanceBy(1);
                _deviceServiceMock.Verify(m => m.GetStatus(), Times.Exactly(2));
                
                _schedulerProvider.AdvanceBy(TimeSpan.FromMinutes(15).Ticks);
                _deviceServiceMock.Verify(m => m.GetStatus(), Times.Exactly(3));
                
                //Update with TelemetryConfig
                _devicePropertiesSubject.OnNext(devPropertiesWithNewPeriod);
                
                _schedulerProvider.AdvanceBy(1);
                _deviceServiceMock.Verify(m => m.GetStatus(), Times.Exactly(4));
                
                _schedulerProvider.AdvanceBy(TimeSpan.FromMinutes(5).Ticks);
                _deviceServiceMock.Verify(m => m.GetStatus(), Times.Exactly(5));
            }

            [Fact]
            public void Should_subscribe_to_FanSpeed_from_Cloud()
            {
                _sut.Run();
                
                _schedulerProvider.AdvanceTo(1);
                _cloudServiceMock.Verify(m => m.SetFan());
            }

            [Fact]
            public void Should_set_FanSpeed_from_cloud()
            {
                var fanMsg = new Fan {Number = 0, Speed = 40};

                _sut.Run();

                _schedulerProvider.AdvanceTo(2);
                _fanSubject.OnNext(fanMsg);

                _deviceServiceMock.Verify(m => m.SetFan(fanMsg));
            }
            
            [Fact(Skip = "Don't know how to test resubscription with subject")]
            public void Should_resubscribe_in_30_seconds_when_cloud_set_fan_throws_exception()
            {
                var fanMsg = new Fan {Number = 0, Speed = 40};
                
                _sut.Run();

                _schedulerProvider.AdvanceTo(2);
                _fanSubject.OnError(new Exception());
                
                _fanSubject.OnNext(fanMsg);
                _schedulerProvider.AdvanceBy(TimeSpan.FromSeconds(30).Ticks);
                _fanSubject.OnNext(fanMsg);
                
                _deviceServiceMock.Verify(m => m.SetFan(fanMsg));
            }
            
            [Fact]
            public void Should_resubscribe_in_30_seconds_when_device_set_fan_throws_exception()
            {
                var failingFanMsg = new Fan {Number = 0, Speed = 40};
                var successfullFanMsg = new Fan {Number = 0, Speed = 50};
                //First message will fail
                _deviceServiceMock.Setup(m => m.SetFan(It.IsAny<Fan>()))
                    .ThrowsAsync(new Exception());
                
                _sut.Run();

                _schedulerProvider.AdvanceTo(2);
                _fanSubject.OnNext(failingFanMsg);
                
                //Next message will not fail
                _deviceServiceMock.Setup(m => m.SetFan(It.IsAny<Fan>()))
                    .ReturnsAsync(true);
                
                //Advance to resubscription
                _schedulerProvider.AdvanceBy(TimeSpan.FromSeconds(30).Ticks);
                _fanSubject.OnNext(successfullFanMsg);
                
                _deviceServiceMock.Verify(m => m.SetFan(failingFanMsg));
                _deviceServiceMock.Verify(m => m.SetFan(successfullFanMsg));
            }

            public class LightScheduleTests : RunnerTests
            {
                [Theory]
                [InlineData(1, 3, 1)]
                [InlineData(1, 3, 2)]
                [InlineData(1, 3, 2.9999)]
                [InlineData(3, 1, 3)]
                [InlineData(3, 1, 0)]
                [InlineData(3, 1, 0.9999)]
                public void Should_turn_relay0_on_when_schedule_is_on(double on, double off, double hourOfDay)
                {
                    _clockServiceMock.Setup(m => m.HourOfDay()).Returns(hourOfDay);
                    var devPropertiesWithLightSchedule = new DesiredDeviceProperties
                    {
                        LightSchedule = new LightSchedule
                        {
                            On = on,
                            Off = off
                        }
                    };
                
                    _sut.Run();
                
                    _schedulerProvider.AdvanceTo(2);
                    _devicePropertiesSubject.OnNext(devPropertiesWithLightSchedule);
                
                    _deviceServiceMock.Verify(m => m.SetRelay(It.Is<Relay>(x => x.Number == 0 && x.On)), Times.Exactly(1));
                }
                
                [Theory]
                [InlineData(0, 0, 0)]
                [InlineData(1, 3, 0)]
                [InlineData(1, 3, 3)]
                [InlineData(1, 3, 4)]
                [InlineData(3, 1, 1)]
                [InlineData(3, 1, 2)]
                [InlineData(3, 1, 2.9999)]
                public void Should_turn_relay0_off_when_schedule_is_off(double on, double off, double hourOfDay)
                {
                    _clockServiceMock.Setup(m => m.HourOfDay()).Returns(hourOfDay);
                    var devPropertiesWithLightSchedule = new DesiredDeviceProperties
                    {
                        LightSchedule = new LightSchedule
                        {
                            On = on,
                            Off = off
                        }
                    };
                
                    _sut.Run();
                
                    _schedulerProvider.AdvanceTo(2);
                    _devicePropertiesSubject.OnNext(devPropertiesWithLightSchedule);
                
                    _deviceServiceMock.Verify(m => m.SetRelay(It.Is<Relay>(x => x.Number == 0 && !x.On)), Times.Exactly(1));
                }

                [Theory]
                [InlineData(1, 3, 0, 1)]
                [InlineData(3, 1, 2, 3)]
                public void Should_trun_relay0_on_when_clock_reaches_on_time(double on, double off, double hourOfDay, double tick)
                {
                    _clockServiceMock.Setup(m => m.HourOfDay()).Returns(hourOfDay);
                    var devPropertiesWithLightSchedule = new DesiredDeviceProperties
                    {
                        LightSchedule = new LightSchedule
                        {
                            On = on,
                            Off = off
                        }
                    };
                    _sut.Run();

                    _schedulerProvider.AdvanceTo(2);
                    _devicePropertiesSubject.OnNext(devPropertiesWithLightSchedule);
                    
                    _deviceServiceMock.Verify(m => m.SetRelay(It.Is<Relay>(x => x.Number == 0 && !x.On)),
                        Times.Exactly(1));
                    
                    _clockTickSubject.OnNext(tick);

                    _deviceServiceMock.Verify(m => m.SetRelay(It.Is<Relay>(x => x.Number == 0 && x.On)),
                        Times.Exactly(1));
                }
                
                [Theory]
                [InlineData(1, 3, 2, 3)]
                [InlineData(3, 1, 0, 1)]
                public void Should_trun_relay0_off_when_clock_reaches_off_time(double on, double off, double hourOfDay, double tick)
                {
                    _clockServiceMock.Setup(m => m.HourOfDay()).Returns(hourOfDay);
                    var devPropertiesWithLightSchedule = new DesiredDeviceProperties
                    {
                        LightSchedule = new LightSchedule
                        {
                            On = on,
                            Off = off
                        }
                    };
                    _sut.Run();

                    _schedulerProvider.AdvanceTo(2);
                    _devicePropertiesSubject.OnNext(devPropertiesWithLightSchedule);
                    
                    _deviceServiceMock.Verify(m => m.SetRelay(It.Is<Relay>(x => x.Number == 0 && x.On)),
                        Times.Exactly(1));
                    
                    _clockTickSubject.OnNext(tick);

                    _deviceServiceMock.Verify(m => m.SetRelay(It.Is<Relay>(x => x.Number == 0 && !x.On)),
                        Times.Exactly(1));
                }
                
                [Theory]
                [InlineData(1, 3, 1, 2)]
                [InlineData(3, 1, 3, 4)]
                public void Shouldnt_trun_relay0_on_when_clock_reaches_on_time_but_relay_status_isnt_changing(double on, double off, double hourOfDay, double tick)
                {
                    _clockServiceMock.Setup(m => m.HourOfDay()).Returns(hourOfDay);
                    var devPropertiesWithLightSchedule = new DesiredDeviceProperties
                    {
                        LightSchedule = new LightSchedule
                        {
                            On = on,
                            Off = off
                        }
                    };
                    _sut.Run();

                    _schedulerProvider.AdvanceTo(2);
                    _devicePropertiesSubject.OnNext(devPropertiesWithLightSchedule);
                    
                    _deviceServiceMock.Verify(m => m.SetRelay(It.Is<Relay>(x => x.Number == 0 && x.On)),
                        Times.Exactly(1));
                    
                    _clockTickSubject.OnNext(tick);

                    _deviceServiceMock.Verify(m => m.SetRelay(It.Is<Relay>(x => x.Number == 0 && x.On)),
                        Times.Exactly(1));
                }
                
                [Theory]
                [InlineData(1, 3, 3, 4)]
                [InlineData(3, 1, 1, 2)]
                public void Shouldnt_trun_relay0_off_when_clock_reaches_off_time_but_relay_status_isnt_changing(double on, double off, double hourOfDay, double tick)
                {
                    _clockServiceMock.Setup(m => m.HourOfDay()).Returns(hourOfDay);
                    var devPropertiesWithLightSchedule = new DesiredDeviceProperties
                    {
                        LightSchedule = new LightSchedule
                        {
                            On = on,
                            Off = off
                        }
                    };
                    _sut.Run();

                    _schedulerProvider.AdvanceTo(2);
                    _devicePropertiesSubject.OnNext(devPropertiesWithLightSchedule);
                    
                    _deviceServiceMock.Verify(m => m.SetRelay(It.Is<Relay>(x => x.Number == 0 && !x.On)),
                        Times.Exactly(1));
                    
                    _clockTickSubject.OnNext(tick);

                    _deviceServiceMock.Verify(m => m.SetRelay(It.Is<Relay>(x => x.Number == 0 && !x.On)),
                        Times.Exactly(1));
                }
            }

            public class DeviceFirmwareUpdaterSubscribe : RunnerTests
            {
                [Fact]
                public void Should_call_subscribe()
                {
                    _sut.Run();
                    _schedulerProvider.AdvanceTo(2);
                    
                    _deviceFirmwareUpdaterMock.VerifyGet(m => m.UpdateFirmware);
                }
            }
        }
    }
}