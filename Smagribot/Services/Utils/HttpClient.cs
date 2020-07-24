using System.IO;
using System.Threading.Tasks;

namespace Smagribot.Services.Utils
{
    public interface IHttpClient
    {
        Task SafeFile(string requestUri, string toPath);
    }
    
    public class HttpClient : System.Net.Http.HttpClient, IHttpClient
    {
        public async Task SafeFile(string requestUri, string toPath)
        {
            using (var fs = File.Create(toPath))
            {
                var stream = await GetStreamAsync(requestUri);
                await stream.CopyToAsync(fs);
            }
        }
    }
}