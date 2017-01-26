using System;
using System.Threading.Tasks;
using Rebus.Logging;
using Rebus.Threading;
using Rebus.Threading.SystemTimersTimer;

namespace Rebus.Tests.Timers.Factories
{
    public class TimerTaskFactory : IAsyncTaskFactory
    {
        public IAsyncTask CreateTask(TimeSpan interval, Func<Task> action)
        {
            var asyncTask = new TimerAsyncTask("task", action, new ConsoleLoggerFactory(false), false)
            {
                Interval = interval
            };

            return asyncTask;
        }
    }
}