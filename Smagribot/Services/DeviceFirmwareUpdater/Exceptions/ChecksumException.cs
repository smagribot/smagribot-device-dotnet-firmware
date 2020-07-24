using System;

namespace Smagribot.Services.DeviceFirmwareUpdater.Exceptions
{
    public class ChecksumException : Exception
    {
        public ChecksumException()
        {   
        }

        public ChecksumException(string message) : base(message)
        {
        }
    }
}