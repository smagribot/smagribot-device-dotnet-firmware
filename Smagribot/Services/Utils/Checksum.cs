using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace Smagribot.Services.Utils
{
    public interface IChecksum
    {
        Task<string> Md5(string filepath);
    }
    
    public class Checksum : IChecksum
    {
        public Task<string> Md5(string filepath)
        {
            return Task.Run(() =>
            {
                using (var md5 = MD5.Create())
                {
                    using (var stream = File.OpenRead(filepath))
                    {
                        return Convert.ToBase64String(md5.ComputeHash(stream));
                    }
                }
            });
        }
    }
}