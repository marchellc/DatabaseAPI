using System;
using System.Collections.Concurrent;
using System.IO;
using System.Timers;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using DatabaseAPI.Logging;
using Microsoft.Win32;
using Timer = System.Timers.Timer;

namespace DatabaseAPI.IO;

public class DatabaseMonitor : IDisposable
{
    private volatile string _filePath;
    
    private volatile bool _fileStatus;
    private volatile bool _changeStatus;

    private volatile Timer _timer;
    private volatile FileSystemWatcher _watcher;

    public bool IsSavingOrReading => _fileStatus;
    public bool IsChanged => _changeStatus;

    public string FilePath
    {
        get => _filePath;
        set => _filePath = Path.GetFullPath(value);
    }

    public event Func<Task> OnWriteAsync;
    public event Func<Task> OnReadAsync;

    public DatabaseMonitor(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) 
            throw new ArgumentNullException(nameof(filePath));
        
        FilePath = filePath;
    }

    public void Start(int checkInterval)
    {
        if (checkInterval <= 0) throw new ArgumentOutOfRangeException(nameof(checkInterval));
        
        _watcher = new FileSystemWatcher(Path.GetDirectoryName(FilePath));
        _watcher.Changed += OnChanged;

        _watcher.NotifyFilter = NotifyFilters.Size | NotifyFilters.LastWrite;
        _watcher.EnableRaisingEvents = true;
        
        _timer = new Timer(checkInterval);
        _timer.Elapsed += UpdateTimer;
        
        _timer.Start();
        
        Log.Debug("Database Monitor", $"Started.");
    }

    public void Register()
    {
        if (_changeStatus) 
            return;
        
        Log.Debug("Database Monitor", $"Registering change");
        
        _changeStatus = true;
    }

    public void Dispose()
    {
        Log.Debug("Database Monitor", "Disposing");

        if (_timer != null)
        {
            _timer.Elapsed -= UpdateTimer;
            
            _timer.Stop();
            _timer.Dispose();
            
            _timer = null;
        }

        if (_watcher != null)
        {
            _watcher.Changed -= OnChanged;
            
            _watcher.Dispose();
            _watcher = null;
        }

        _fileStatus = false;
        _changeStatus = false;
    }

    private async Task SaveFileAsync()
    {
        Log.Debug("Database Monitor", "SaveFile called");
        
        if (OnWriteAsync is null)
        {
            Log.Debug("Database Monitor", "SaveFile while OnWrite is null");
            return;
        }
        
        _fileStatus = true;

        try
        {
            await OnWriteAsync();
        }
        catch (Exception ex)
        {
            Log.Error("Database Monitor", $"OnWrite error:\n{ex}");
        }
        finally
        {
            await Task.Delay(2500);

            _fileStatus = false;
        }
    }

    private async Task ReadFileAsync()
    {
        Log.Debug("Database Monitor", "ReadFile called");
        
        if (OnReadAsync is null)
        {
            Log.Debug("Database Monitor", "ReadFile while OnRead is null");
            return;
        }
        
        _fileStatus = true;

        try
        {
            await OnReadAsync();
        }
        catch (Exception ex)
        {
            Log.Error("Database Monitor", $"OnRead error:\n{ex}");
        }
        finally
        {
            await Task.Delay(2500);

            _fileStatus = false;
        }
    }

    private async void UpdateTimer(object _, ElapsedEventArgs ev)
    {
        if (_fileStatus) 
            return;
        
        if (_changeStatus)
        {
            _changeStatus = false;

            await SaveFileAsync();
        }
    }

    private async void OnChanged(object _, FileSystemEventArgs ev)
    {
        if (_fileStatus) return;
        if (ev.FullPath != _filePath) return;

        Log.Debug("Database Monitor", $"File changed");

        await ReadFileAsync();
    }
}