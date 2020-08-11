using System.Reactive.Concurrency;
using Microsoft.Reactive.Testing;
using Smagribot.Services.Scheduler;

namespace Smagribot.Tests.TestHelpers
{
    public class TestSchedulerProvider : TestScheduler, ISchedulerProvider
    {
        public IScheduler NewThread { get; }
        public IScheduler TaskPool { get; }
        public IScheduler ThreadPool { get; }

        public TestSchedulerProvider()
        {
            NewThread = this;
            TaskPool = this;
            ThreadPool = this;
        }
    }
}