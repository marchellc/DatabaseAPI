using System;
using System.Threading;

namespace DatabaseAPI.IO.Locks;

public class StatusLock : IDisposable
{
    private volatile bool _isLocked;
    private volatile AutoResetEvent _resetEvent;

    public bool IsLocked => _isLocked;

    public StatusLock()
    {
        _isLocked = false;
        _resetEvent = new AutoResetEvent(true);
    }

    public void Trigger()
    {
        _resetEvent.WaitOne();
        _isLocked = true;
    }

    public void Release()
    {
        _isLocked = false;
        _resetEvent.Set();
    }

    public void TriggerAndRelease(Action action)
    {
        Trigger();

        var ex = default(Exception);

        try
        {
            action();
        }
        catch (Exception e)
        {
            ex = e;
        }
        finally
        {
            Release();
        }

        if (ex != null)
            throw ex;
    }
    
    public T TriggerAndRelease<T>(Func<T> action)
    {
        Trigger();

        var ex = default(Exception);
        var result = default(T);

        try
        {
            result = action();
        }
        catch (Exception e)
        {
            ex = e;
        }
        finally
        {
            Release();
        }

        if (ex != null)
            throw ex;

        return result;
    }

    public void Dispose()
    {
        _isLocked = false;
        
        _resetEvent?.Dispose();
        _resetEvent = null;
    }
}