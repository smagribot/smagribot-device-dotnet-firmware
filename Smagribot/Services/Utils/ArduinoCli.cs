using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Smagribot.Services.Utils
{
    public class ArduinoCliException : Exception
    {
        public ArduinoCliException()
        {
        }

        public ArduinoCliException(string message) : base(message)
        {
        }
    }
    
    public interface IArduinoCli
    {
        Task Upload(string pathToFirmware, string port, string fqbn);
    }
    
    public class ArduinoCli : IArduinoCli
    {
        private readonly string _pathToArduinoCli;
        
        public ArduinoCli(string pathToArduinoCli)
        {
            _pathToArduinoCli = pathToArduinoCli;
        }
        
        /*
         * arduino-cli upload [flags]
         *
         * Options:
         * -b, --fqbn string    Fully Qualified Board Name, e.g.: arduino:avr:uno
         * -h, --help           help for upload
         * -i, --input string   Input file to be uploaded.
         * -p, --port string    Upload port, e.g.: COM10 or /dev/ttyACM0
         * -t, --verify         Verify uploaded binary after the upload.
         *
         *  See https://arduino.github.io/arduino-cli/commands/arduino-cli_upload/
         */
        public Task Upload(string pathToFirmware, string port, string fqbn)
        {
            return Task.Run(() =>
            {
                var startInfo = new ProcessStartInfo
                {
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    FileName = _pathToArduinoCli,
                    Arguments = $"upload -p {port} -b {fqbn} -i {pathToFirmware} -t"
                };
                var process = new Process
                {
                    StartInfo = startInfo
                };
                process.Start();
                var errorOutput = process.StandardError.ReadToEnd();  
                process.WaitForExit();
                if (process.ExitCode != 0)
                    throw new ArduinoCliException(errorOutput);
            });
        }
    }
}