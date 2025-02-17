using System;
using System.Threading.Tasks;

namespace DatabaseAPI;

public static class ThreadUtils
{
    [ThreadStatic]
    private static TaskScheduler currentScheduler;
    private static volatile TaskScheduler mainScheduler;

    public static TaskScheduler Scheduler => currentScheduler ??= TaskScheduler.FromCurrentSynchronizationContext();
    public static TaskScheduler MainScheduler => mainScheduler;

    public static void SetMainScheduler() => mainScheduler = TaskScheduler.FromCurrentSynchronizationContext();

    public static void RunOnMain(this Action action, Action onCompleted = null, Action<Exception> onError = null)
    {
        if (action is null) throw new ArgumentNullException(nameof(action));
        if (mainScheduler is null) throw new Exception("Main Scheduler has not been set.");

        var task = new Task(action);

        if (onCompleted != null || onError != null)
        {
            task.ContinueWith(_ =>
            {
                if (task.Exception != null && onError != null) onError(task.Exception);
                if (task.Exception is null && onCompleted != null) onCompleted();
            });
        }
        
        task.Start(mainScheduler);
    }

    public static void ContinueWithMain(this Task task, Action onCompleted, Action<Exception> onError = null)
    {
        if (task is null) throw new ArgumentNullException(nameof(task));
        if (onCompleted is null) throw new ArgumentNullException(nameof(onCompleted));
        if (mainScheduler is null) throw new Exception("Main Scheduler has not been set.");

        task.ContinueWith(_ =>
        {   
            if (task.Exception != null) onError?.Invoke(task.Exception);
            else onCompleted();
        }, mainScheduler);
    }
    
    public static void ContinueWithMain<T>(this Task<T> task, Action<T> onCompleted, Action<Exception> onError = null)
    {
        if (task is null) throw new ArgumentNullException(nameof(task));
        if (onCompleted is null) throw new ArgumentNullException(nameof(onCompleted));
        if (mainScheduler is null) throw new Exception("Main Scheduler has not been set.");

        task.ContinueWith(_ =>
        {   
            if (task.Exception != null) onError?.Invoke(task.Exception);
            else onCompleted(task.Result);
        }, mainScheduler);
    }
}