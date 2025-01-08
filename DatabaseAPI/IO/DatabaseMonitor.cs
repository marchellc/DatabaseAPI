using System;
using System.IO;
using System.Timers;
using System.Diagnostics;
using System.Threading.Tasks;
using DatabaseAPI.Logging;
using Microsoft.Win32;

namespace DatabaseAPI.IO;

public class DatabaseMonitor : IDisposable
{
    private volatile string _filePath;
    private volatile bool _fileStatus;
    
    private volatile int _minorChanges = 0;
    private volatile int _majorChanges = 0;

    private volatile int _reqMinorChanges = 0;
    private volatile int _reqMajorChanges = 0;

    private volatile int _maxNoMinorChangeTime = -1;
    private volatile int _maxNoMajorChangeTime = -1;

    private volatile Timer _timer;
    private volatile FileSystemWatcher _watcher;

    private volatile Stopwatch _majorWatch;
    private volatile Stopwatch _minorWatch;

    public bool IsSavingOrReading => _fileStatus;
    
    public int MinorChanges => _minorChanges;
    public int MajorChanges => _majorChanges;

    public int MinorChangesRequired
    {
        get => _reqMinorChanges;
        set => _reqMinorChanges = value;
    }

    public int MajorChangesRequired
    {
        get => _reqMinorChanges;
        set => _reqMajorChanges = value;
    }

    public int MaxNoMinorChangeTime
    {
        get => _maxNoMinorChangeTime;
        set => _maxNoMinorChangeTime = value;
    }

    public int MaxNoMajorChangeTime
    {
        get => _maxNoMajorChangeTime;
        set => _maxNoMajorChangeTime = value;
    }

    public string FilePath
    {
        get => _filePath;
        set => _filePath = value;
    }

    public event Action OnWrite;
    public event Action OnRead;

    public DatabaseMonitor(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentNullException(nameof(filePath));

        _filePath = filePath;
        
        _watcher = new FileSystemWatcher(Path.GetDirectoryName(filePath));
        _watcher.Changed += OnChanged;

        _watcher.NotifyFilter = NotifyFilters.Size | NotifyFilters.LastWrite;
        _watcher.EnableRaisingEvents = true;
    }

    public void Start()
    {
        _majorWatch = new Stopwatch();
        _minorWatch = new Stopwatch();
        
        _timer = new Timer(100);
        _timer.Elapsed += UpdateTimer;
        
        _timer.Start();
    }

    public void Reset()
    {
        _minorChanges = 0;
        _majorChanges = 0;

        _majorWatch.Restart();
        _minorWatch.Restart();
    }

    public void Register(bool isMinor)
    {
        if (isMinor)
        {
            _minorChanges++;
            _minorWatch.Restart();
        }
        else
        {
            _majorChanges++;
            _majorWatch.Restart();
        }
    }

    public void Dispose()
    {
        OnWrite = null;
        OnRead = null;

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
        
        _minorWatch?.Stop();
        _minorWatch = null;

        _majorWatch?.Stop();
        _majorWatch = null;

        _fileStatus = false;

        _minorChanges = 0;
        _majorChanges = 0;
    }

    private void SaveFile()
    {
        if (OnWrite is null)
            return;
        
        _fileStatus = true;

        try
        {
            OnWrite();
        }
        catch (Exception ex)
        {
            Log.Error("Database Monitor", $"OnWrite error:\n{ex}");
        }
        finally
        {
            Task.Run(async () => await Task.Delay(1000)).ContinueWith(_ => _fileStatus = false);
        }
    }

    private void ReadFile()
    {
        if (OnRead is null)
            return;
        
        _fileStatus = true;

        try
        {
            OnRead();
        }
        catch (Exception ex)
        {
            Log.Error("Database Monitor", $"OnRead error:\n{ex}");
        }
        finally
        {
            Task.Run(async () => await Task.Delay(1000)).ContinueWith(_ => _fileStatus = false);
        }
    }

    private void UpdateTimer(object _, ElapsedEventArgs ev)
    {
        if (_fileStatus)
            return;

        if ((_majorChanges >= _reqMajorChanges || (_maxNoMajorChangeTime > 0 && _majorWatch.ElapsedMilliseconds >= _maxNoMajorChangeTime))
            || (_minorChanges >= _reqMinorChanges || (_maxNoMinorChangeTime > 0 && _minorWatch.ElapsedMilliseconds >= _maxNoMinorChangeTime)))
        {
            Log.Debug("Database Monitor", $"Saving database file due to change count or timeout (Major={_majorChanges} / {_reqMajorChanges}, Minor={_minorChanges} / {_reqMajorChanges})");
            
            Reset();
            SaveFile();
        }
    }

    private void OnChanged(object _, FileSystemEventArgs ev)
    {
        if (_fileStatus)
            return;

        if (ev.FullPath != _filePath)
            return;

        Log.Debug("Database Monitor", $"File changed");

        ReadFile();
    }
}