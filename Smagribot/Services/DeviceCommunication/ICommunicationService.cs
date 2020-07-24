using System.Threading.Tasks;

namespace Smagribot.Services.DeviceCommunication
{
    public interface ICommunicationService
    {
        Task Start();
        Task Stop();
        Task<string> Send(string command);
    }
}