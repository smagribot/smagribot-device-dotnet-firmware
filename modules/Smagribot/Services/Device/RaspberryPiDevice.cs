// using System;
// using System.Device.Gpio;
// using System.Linq;
// using System.Threading.Tasks;
// using Iot.Device.DHTxx;
// using Iot.Device.Hcsr04;
// using Iot.Device.OneWire;
// using Smagribot.Models.Messages;
// using Smagribot.Models.Methods;
//
// namespace Smagribot.Services.Device
// {
//     public class SensorConfiguration
//     {
//         /// <summary>
//         /// Fill sensor switch pin.
//         /// Will be set to input pull up.
//         /// </summary>
//         public int FillSensorPin { get; set; }
//         
//         /// <summary>
//         /// Trigger pin for HC-SR 04 distance sensor.
//         /// </summary>
//         public int Hcsr04TriggerPin { get; set; }
//         
//         /// <summary>
//         /// Echo pin for HC-SR 04 distance sensor.
//         /// </summary>
//         public int Hcsr04EchoPin { get; set; }
//         
//         /// <summary>
//         /// Relay 0 pin.
//         /// </summary>
//         public int Relay0Pin { get; set; }
//         
//         /// <summary>
//         /// Relay 1 pin.
//         /// </summary>
//         public int Relay1Pin { get; set; }
//
//         public int Dht22Pin { get; set; }
//
//         // /// <summary>
//         // /// Pin for DS18B20 temperature sensor.
//         // /// Is using 1-wire protocol, see https://github.com/dotnet/iot/blob/master/src/devices/OneWire/README.md how to setup.
//         // /// The default gpio is 4 (pin 7).
//         // /// </summary>
//         // public int DS18B20Pin { get; set; }
//     }
//     
//     public class RaspberryPiDevice : IDeviceService, IDisposable
//     {
//         private readonly SensorConfiguration _configuration;
//         private readonly GpioController _controller;
//         private readonly Hcsr04 _sonar;
//         private readonly int[] _relayPins = new int[2];
//         private readonly Dht22 _dht22;
//
//         public RaspberryPiDevice(SensorConfiguration configuration)
//         {
//             _configuration = configuration;
//             _controller = new GpioController();
//             
//             _controller.SetPinMode(_configuration.FillSensorPin, PinMode.InputPullUp);
//             _sonar = new Hcsr04(_configuration.Hcsr04TriggerPin, _configuration.Hcsr04EchoPin);
//             _controller.SetPinMode(_configuration.Relay0Pin, PinMode.Output);
//             _controller.SetPinMode(_configuration.Relay1Pin, PinMode.Output);
//             _relayPins[0] = _configuration.Relay0Pin;
//             _relayPins[1] = _configuration.Relay1Pin;
//             _dht22 = new Dht22(_configuration.Dht22Pin);
//         }
//
//         public Task Connect()
//         {
//             return Task.CompletedTask;
//         }
//
//         public Task Disconnect()
//         {
//             return Task.CompletedTask;
//         }
//
//         public async Task<DeviceStatus> GetStatus()
//         {
//             var status = new DeviceStatus();
//
//             await Task.WhenAll(AddFill(status), AddHumidityAndTemperature(status), AddWaterTemp(status),
//                 AddFanSpeed(status));
//             return status;
//         }
//
//         private Task AddFanSpeed(DeviceStatus status)
//         {
//             return Task.CompletedTask;
//         }
//
//         private Task AddHumidityAndTemperature(DeviceStatus status)
//         {
//             return Task.Run(() =>
//             {
//                 status.Temp = Convert.ToSingle(_dht22.Temperature.Celsius);
//                 status.Humidity = Convert.ToSingle(_dht22.Humidity);
//             });
//         }
//
//         private async Task AddWaterTemp(DeviceStatus status)
//         {
//             var temperature = await OneWireThermometerDevice.EnumerateDevices().First().ReadTemperatureAsync();
//             status.WaterTemp = Convert.ToSingle(temperature.Celsius);
//             
//             // // More advanced way, with rescanning the bus and iterating devices per 1-wire bus
//             // foreach (var busId in OneWireBus.EnumerateBusIds())
//             // {
//             //     var bus = new OneWireBus(busId);
//             //     Console.WriteLine($"Found bus '{bus.BusId}', scanning for devices ...");
//             //     await bus.ScanForDeviceChangesAsync();
//             //     foreach (var devId in bus.EnumerateDeviceIds())
//             //     {
//             //         var dev = new OneWireDevice(busId, devId);
//             //         Console.WriteLine($"Found family '{dev.Family}' device '{dev.DeviceId}' on '{bus.BusId}'");
//             //         if (OneWireThermometerDevice.IsCompatible(busId, devId))
//             //         {
//             //             var devTemp = new OneWireThermometerDevice(busId, devId);
//             //             Console.WriteLine("Temperature reported by device: " +
//             //                               (await devTemp.ReadTemperatureAsync()).Celsius.ToString("F2") +
//             //                               "\u00B0C");
//             //         }
//             //     }
//             // }
//         }
//
//         private async Task AddFill(DeviceStatus status)
//         {
//             var pinValue = await Task.Run(() => _controller.Read(_configuration.FillSensorPin));
//             status.Fill = pinValue.Equals(PinValue.High);
//         }
//
//         public Task<Version> GetFirmware()
//         {
//             return Task.FromResult(Version.Parse("0.0.1"));
//         }
//
//         public Task<bool> SetFan(Fan fan)
//         {
//             return Task.FromResult(false);
//         }
//
//         public Task<bool> SetRelay(Relay relay)
//         {
//             return Task.Run(() =>
//             {
//                 if (relay.On)
//                 {
//                     _controller.OpenPin(_relayPins[relay.Number]);
//                 }
//                 else
//                 {
//                     _controller.ClosePin(_relayPins[relay.Number]);
//                 }
//
//                 return true;
//             });
//         }
//
//         public Task<double> GetDistance()
//         {
//             return Task.Run(() => _sonar.Distance);
//         }
//
//         public void Dispose()
//         {
//             _controller?.Dispose();
//             _sonar?.Dispose();
//             _dht22?.Dispose();
//         }
//     }
// }