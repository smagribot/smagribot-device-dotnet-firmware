using MMALSharp.Common;
using Smagribot.Models.DeviceProperties;

namespace Camera.Extensions
{
    public static class MMALEncodingExtensions
    {
        public static MMALEncoding CreateMMALEncoding(this EncodingFormat encodingFormat)
        {
            switch (encodingFormat)
            {
                case EncodingFormat.BMP:
                    return MMALEncoding.BMP;
                case EncodingFormat.JPEG:
                default:
                    return MMALEncoding.JPEG;
            }
        }

        public static MMALEncoding CreateMMALEncoding(this PixelFormat pixelFormat)
        {
            switch (pixelFormat)
            {
                case PixelFormat.RGBA:
                    return MMALEncoding.RGBA;
                case PixelFormat.I420:
                default:
                    return MMALEncoding.I420;
            }
        }
    }
}