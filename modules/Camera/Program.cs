using System;
using System.Threading;
using Autofac;
using Camera.Services.Camera;
using Microsoft.Extensions.Logging;
using MMALSharp.Common.Utility;
using Smagribot.Services.Cloud;
using Smagribot.Services.Scheduler;
using Smagribot.Services.Utils;

namespace Camera
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
            Console.WriteLine(" _______ __                  __                          \n" +
                              "|_     _|__|.--------.-----.|  |.---.-.-----.-----.-----.\n" +
                              "  |   | |  ||        |  -__||  ||  _  |  _  |__ --|  -__|\n" +
                              "  |___| |__||__|__|__|_____||__||___._|   __|_____|_____|\n" +
                              "                                      |__|   ");
            Console.WriteLine("Smagribot Timelapse Camera 🌱📷 Console runner");
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
            
            MMALLog.LoggerFactory = loggerFactory;
            
            builder.RegisterInstance(logger)
                .As<ILogger>()
                .SingleInstance();

            builder.RegisterType<SchedulerProvider>()
                .As<ISchedulerProvider>()
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

            builder.RegisterType<RpiCameraModule>()
                .As<ICameraService>()
                .SingleInstance();
            
            builder.RegisterType<Runner>()
                .SingleInstance();

            builder.RegisterType<ClockService>()
                .As<IClockService>()
                .SingleInstance();

            Container = builder.Build();
        }
    }
}