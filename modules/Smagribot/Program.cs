using System;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Microsoft.Extensions.Logging;
using Moq;
using Smagribot.Models.Messages;
using Smagribot.Models.Methods;
using Smagribot.Services.Cloud;
using Smagribot.Services.Device;
using Smagribot.Services.DeviceCommunication;
using Smagribot.Services.DeviceFirmwareUpdater;
using Smagribot.Services.Parser;
using Smagribot.Services.Scheduler;
using Smagribot.Services.Utils;

namespace Smagribot
{
    class Program
    {
        public static IContainer Container { get; set; }

        private static readonly AutoResetEvent WaitHandle = new AutoResetEvent(false);
        
        static void Main(string[] args)
        {
            Console.WriteLine("                                                                                           \n" +
                              "                                                          ,,   ,,                          \n" +
                              " .M\"\"\"bgd                                                 db  *MM                    mm    \n" +
                              ",MI    \"Y                                                      MM                    MM    \n" +
                              "`MMb.     `7MMpMMMb.pMMMb.   ,6\"Yb.   .P\"Ybmmm `7Mb,od8 `7MM   MM,dMMb.   ,pW\"Wq.  mmMMmm  \n" +
                              "  `YMMNq.   MM    MM    MM  8)   MM  :MI  I8     MM' \"'   MM   MM    `Mb 6W'   `Wb   MM    \n" +
                              ".     `MM   MM    MM    MM   ,pm9MM   WmmmP\"     MM       MM   MM     M8 8M     M8   MM    \n" +
                              "Mb     dM   MM    MM    MM  8M   MM  8M          MM       MM   MM.   ,M9 YA.   ,A9   MM    \n" +
                              "P\"Ybmmd\"  .JMML  JMML  JMML.`Moo9^Yo. YMMMMMb  .JMML.   .JMML. P^YbmdP'   `Ybmd9'    `Mbmo \n" +
                              "                                     6'     dP                                             \n" +
                              "                                     Ybmmmd'                                                ");
            Console.WriteLine("Smagribot 🌱 Console runner");
            Console.WriteLine($"Version: {typeof(Program).Assembly.GetName().Version}");

            SetupIoC();

            var runner = Container.Resolve<Runner>();
            runner.Run();
            
            Console.CancelKeyPress += (o, e) =>
            {
                Console.WriteLine("Exit");
                WaitHandle.Set();
            };

            WaitHandle.WaitOne();
            runner.Dispose();
        }

        private static void SetupIoC()
        {
            var builder = new ContainerBuilder();
            
            var loggerFactory = LoggerFactory.Create(loggerBuilder =>
            {
                var logLevelEnv = Environment.GetEnvironmentVariable("LogLevel");
                
#if DEBUG
                var defaultLogLevel = LogLevel.Debug;
#else
                var defaultLogLevel = LogLevel.Information;
#endif
                switch (logLevelEnv?.ToLower())
                {
                    case "none":
                        defaultLogLevel = LogLevel.None;
                        break;
                    case "debug":
                        defaultLogLevel = LogLevel.Debug;
                        break;
                    case "information":
                        defaultLogLevel = LogLevel.Information;
                        break;
                }
                
                loggerBuilder
                    .SetMinimumLevel(defaultLogLevel)
                    .AddConsole(c => { c.TimestampFormat = "[HH:mm:ss] "; })
                    .AddDebug();
            });

            var logger = loggerFactory.CreateLogger("SmagriBot Controller");
            builder.RegisterInstance(logger)
                .As<ILogger>()
                .SingleInstance();
         
            builder.RegisterType<SchedulerProvider>()
                .As<ISchedulerProvider>()
                .SingleInstance();
            
            var serialPortName = Environment.GetEnvironmentVariable("SerialPortName") ?? "/dev/ttyACM0" ;
            var serialBaudRate = int.Parse(Environment.GetEnvironmentVariable("SerialBaudRate") ?? "9600");
            builder.RegisterType<SerialCommunicationService>()
                .WithParameter("portName", serialPortName)
                .WithParameter("baudRate", serialBaudRate)
                .As<ICommunicationService>()
                .SingleInstance();

            if (Environment.GetEnvironmentVariable("IsEdgeDevice") != null)
            {
                builder.RegisterType<AzureIoTEdgeService>()
                    .As<ICloudService>()
                    .SingleInstance();
            }
            else
            {
                var azureIotHubConnectionString = Environment.GetEnvironmentVariable("AzureIotHubConnectionString");
                builder.RegisterType<AzureIoTHubService>()
                    .WithParameter("connectionString", azureIotHubConnectionString)
                    .As<ICloudService>()
                    .SingleInstance();
            }

            if (bool.TryParse(Environment.GetEnvironmentVariable("UseDeviceMock"), out var useMockedDevice) && useMockedDevice)
            {
                logger.LogInformation("UseDeviceMock found! Setting up Mocked Device!");
                SetupDeviceMock(logger, builder);
            }
            else
            {
                builder.RegisterType<SmagriBotDevice>()
                    .As<IDeviceService>()
                    .SingleInstance();
            }

            builder.RegisterType<SerialDeviceResultParser>()
                .As<IDeviceResultParser>()
                .SingleInstance();
            
            builder.RegisterType<Runner>()
                .SingleInstance();

            builder.RegisterType<ClockService>()
                .As<IClockService>()
                .SingleInstance();

            builder.RegisterType<HttpClient>()
                .As<IHttpClient>();

            builder.RegisterType<Checksum>()
                .As<IChecksum>()
                .SingleInstance();

            var firmwareDownloadPath = Environment.GetEnvironmentVariable("FirmwareDownloadPath");
            var fqbn = Environment.GetEnvironmentVariable("FQBN") ?? "arduino:avr:uno";
            if (bool.TryParse(Environment.GetEnvironmentVariable("UseArduinoCliMock"), out var useArduinoCliMock) && useArduinoCliMock)
            {
                logger.LogInformation("UseArduinoCliMock found! Setting up Mocked arduino-cli!");
                var mocked = new Mock<IArduinoCli>();
                mocked.Setup(m => m.Upload(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                    .Callback<string, string, string>( (pathToFirmware, port, propFqbn) => 
                        logger.LogInformation($"Mock ArduinoCli: Upload(pathToFirmware: {pathToFirmware}, port: {port}, fqbn: {propFqbn}) called"))
                    .Returns(Task.Delay(TimeSpan.FromSeconds(5)));

                builder.RegisterInstance(mocked.Object)
                    .As<IArduinoCli>();
            }
            else
            {
                var arduinoCliPath = Environment.GetEnvironmentVariable("ArduinoCliPath");
                builder.RegisterType<ArduinoCli>()
                    .As<IArduinoCli>()
                    .WithParameter("pathToArduinoCli", arduinoCliPath)
                    .SingleInstance();
            }
            
            builder.RegisterType<ArduinoSerialDeviceFirmwareUpdater>()
                .As<IDeviceFirmwareUpdater>()
                .WithParameter("firmwareDownloadPath", firmwareDownloadPath)
                .WithParameter("serialPortName", serialPortName)
                .WithParameter("fqbn", fqbn)
                .SingleInstance();

            Container = builder.Build();
        }

        private static void SetupDeviceMock(ILogger logger, ContainerBuilder builder)
        {
            var mockedDeviceMock = new Mock<IDeviceService>();
            mockedDeviceMock.Setup(m => m.Connect())
                .Callback(() => logger.LogInformation("Mock Device: Connect() called"))
                .Returns(Task.Delay(TimeSpan.FromSeconds(5)));
            mockedDeviceMock.Setup(m => m.Disconnect())
                .Callback(() => logger.LogInformation("Mock Device: Disconnect() called"))
                .Returns(Task.Delay(TimeSpan.FromSeconds(5)));
            mockedDeviceMock.Setup(m => m.SetFan(It.IsAny<Fan>()))
                .Callback<Fan>(fan => logger.LogInformation($"Mock Device: SetFan({fan}) called"))
                .Returns(async () => await Observable.Timer(TimeSpan.FromSeconds(5)).Select(_ => true).Take(1));
            mockedDeviceMock.Setup(m => m.SetRelay(It.IsAny<Relay>()))
                .Callback<Relay>(relay => logger.LogInformation($"Mock Device: SetRelay({relay}) called"))
                .Returns(async () => await Observable.Timer(TimeSpan.FromSeconds(5)).Select(_ => true).Take(1));
            mockedDeviceMock.Setup(m => m.GetStatus())
                .Callback(() => logger.LogInformation("Mock Device: GetStatus() called"))
                .Returns(async () => await Observable.Timer(TimeSpan.FromSeconds(5)).Select(_ => new DeviceStatus
                {
                    Fill = true,
                    Humidity = 45,
                    Temp = 22.5f,
                    FanSpeed = 0,
                    WaterTemp = 18.5f
                }).Take(1));
            mockedDeviceMock.Setup(m => m.GetFirmware())
                .Callback(() => logger.LogInformation("Mock Device: GetFirmware() called"))
                .Returns(
                    async () => await Observable.Timer(TimeSpan.FromSeconds(5)).Select(_ => Version.Parse("0.0.1")).Take(1));

            builder.RegisterInstance(mockedDeviceMock.Object)
                .As<IDeviceService>();
        }

        private static void SetupRaspberryPiSensorConfiguration()
        {
            // var config = new SensorConfiguration {FillSensorPin = 17};
        }
    }
}
