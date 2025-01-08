using System;
using System.Threading.Tasks;

namespace DatabaseAPI;

public static class ThreadUtils
{
    [ThreadStatic]
    private static TaskScheduler _curScheduler;

    public static TaskScheduler Scheduler
    {
        get
        {
            if (_curScheduler is null)
                _curScheduler = TaskScheduler.FromCurrentSynchronizationContext();

            return _curScheduler;
        }
    }
}