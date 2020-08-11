using System;
using System.Reactive.Linq;

namespace Smagribot.Services.Utils
{
    public interface IClockService
    {
        IObservable<double> Tick { get; }
        double HourOfDay();
    }
    
    public class ClockService : IClockService
    {
        public ClockService()
        {
            Tick = Observable.Interval(TimeSpan.FromMinutes(1))
                .Select(_ => HourOfDay()).Publish().RefCount();
        }
        
        public IObservable<double> Tick { get; }

        public double HourOfDay()
        {
            var now = DateTime.Now;
            var hourOfDayTimeSpan = TimeSpan.FromHours(now.Hour) 
                                    + TimeSpan.FromMinutes(now.Minute) 
                                    + TimeSpan.FromSeconds(now.Second);
            return hourOfDayTimeSpan.TotalHours;
        }
    }
}