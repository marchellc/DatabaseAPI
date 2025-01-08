using System;
using System.IO;
using System.Text;
using System.Collections.Concurrent;

using DatabaseAPI.IO.Locks;

namespace DatabaseAPI.IO;

public class DatabaseFile : IDisposable
{
    private static volatile ConcurrentDictionary<string, DatabaseFile> _files = new ConcurrentDictionary<string, DatabaseFile>();

    public static DatabaseFile GetOrCreate(string filePath, string lockName, string lockDirectory, string lockPrefix = null, Action<DatabaseFile> onCreated = null)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentNullException(nameof(filePath));

        if (string.IsNullOrWhiteSpace(lockName))
            throw new ArgumentNullException(nameof(lockName));

        if (string.IsNullOrWhiteSpace(lockDirectory))
            throw new ArgumentNullException(nameof(lockDirectory));

        filePath = Path.GetFullPath(filePath);
        lockDirectory = Path.GetFullPath(lockDirectory);

        if (_files.TryGetValue(filePath, out var dbFile))
            return dbFile;

        dbFile = new DatabaseFile(filePath, lockName, lockDirectory, lockPrefix);
        dbFile.Initialize();

        onCreated?.Invoke(dbFile);

        _files.TryAdd(filePath, dbFile);
        return dbFile;
    }

    private volatile int _lockWaitMs = -1;

    private volatile bool _lockIgnoreThis;
    private volatile bool _lockMatchPrefix;
    private volatile bool _lockKillOverTime;
    private volatile bool _lockDeleteOverTime;
    private volatile bool _lockAllowSelfKill;

    private volatile bool _read;
    
    private volatile string _path;
    
    private volatile string _lockName;
    private volatile string _lockPrefix;
    private volatile string _lockDirectory;

    private volatile DatabaseMonitor _monitor;
    private volatile StatusLock _threadLock;
    private volatile FileLock _fileLock;

    private volatile ConcurrentDictionary<string, DatabaseTable> _tables = new ConcurrentDictionary<string, DatabaseTable>();

    public string FilePath => _path;
    
    public string LockName => _lockName;
    public string LockPrefix => _lockPrefix;
    public string LockDirectory => _lockDirectory;
    
    public TimeSpan MaxLockTime { get; set; } = TimeSpan.Zero;

    public int LockWaitTime
    {
        get => _lockWaitMs;
        set => _lockWaitMs = value;
    }

    public bool LockIgnoreThisProcess
    {
        get => _lockIgnoreThis;
        set => _lockIgnoreThis = value;
    }

    public bool LockMatchCustomPrefix
    {
        get => _lockMatchPrefix;
        set => _lockMatchPrefix = value;
    }

    public bool LockKillOverTime
    {
        get => _lockKillOverTime;
        set => _lockKillOverTime = value;
    }

    public bool LockDeleteOverTime
    {
        get => _lockDeleteOverTime;
        set => _lockDeleteOverTime = value;
    }

    public bool LockAllowSelfProcessKill
    {
        get => _lockAllowSelfKill;
        set => _lockAllowSelfKill = value;
    }
    
    public bool IsInitialized => _threadLock != null && _fileLock != null;
    public bool IsRead => _read;

    public DatabaseMonitor Monitor => _monitor;
    
    private DatabaseFile(string filePath, string lockName, string lockDirectory, string lockPrefix = null)
    {
        _path = filePath;
        
        _lockName = lockName;
        _lockPrefix = lockPrefix;
        _lockDirectory = lockDirectory;
    }

    public void Initialize()
    {
        if (_threadLock != null && _fileLock != null && _monitor != null)
            return;
        
        if (string.IsNullOrWhiteSpace(FilePath))
            throw new ArgumentNullException(nameof(FilePath));

        if (string.IsNullOrWhiteSpace(LockName))
            throw new ArgumentNullException(nameof(LockName));
        
        if (string.IsNullOrWhiteSpace(LockDirectory))
            throw new ArgumentNullException(nameof(LockDirectory));
        
        _threadLock = new StatusLock();
        
        _monitor = new DatabaseMonitor(_path);

        _monitor.OnRead += Read;
        _monitor.OnWrite += Write;
        
        _monitor.Start();

        if (!Directory.Exists(LockDirectory))
            Directory.CreateDirectory(LockDirectory);
        
        _fileLock = FileLock.GetOrCreate(LockName, LockDirectory, LockPrefix);

        if (!_files.ContainsKey(_path))
            _files.TryAdd(_path, this);
    }

    public void Dispose()
    {
        _files.TryRemove(_path, out _);

        foreach (var table in _tables)
            table.Value.Dispose();

        _tables.Clear();
        _tables = null;

        if (_monitor != null)
        {
            _monitor.OnRead -= Read;
            _monitor.OnWrite -= Write;
            
            _monitor.Dispose();
            _monitor = null;
        }
        
        if (_fileLock != null)
        {
            if (_fileLock.IsLocked)
                _fileLock.Release();

            _fileLock.Dispose();
            _fileLock = null;
        }

        if (_threadLock != null)
        {
            if (_threadLock.IsLocked)
                _threadLock.Release();

            _threadLock.Dispose();
            _threadLock = null;
        }

        _read = false;
    }

    public void Read()
    {
        var shouldWrite = AccessFile(() =>
        {
            return !FileUtils.ReadBinary(_path, 4, reader =>
            {
                var tableCount = reader.ReadInt32();

                if (tableCount < 1)
                {
                    if (_tables.Count > 0)
                    {
                        foreach (var table in _tables)
                            table.Value.Dispose();

                        _tables.Clear();
                    }

                    return;
                }

                for (int i = 0; i < tableCount; i++)
                {
                    var nameLen = reader.ReadInt32();
                    var nameBytes = reader.ReadBytes(nameLen);
                    var nameValue = Encoding.UTF32.GetString(nameBytes);

                    if (_tables.TryGetValue(nameValue, out var table))
                    {
                        table.ReadSelf(reader, true);
                    }
                    else
                    {
                        table = new DatabaseTable(nameValue, this);
                        table.ReadSelf(reader, false);

                        _tables.TryAdd(nameValue, table);
                    }
                }

                _read = true;
            });
        }, true);

        _read = true;

        if (shouldWrite)
            Write();
    }

    public void Write()
    {
        AccessFile(() =>
        {
            FileUtils.CopyIfExists(_path, $"-backup-{DateTime.Now.ToLocalTime().Ticks}");
            FileUtils.WriteBinary(_path, writer =>
            {
                writer.Write(_tables.Count);

                foreach (var table in _tables)
                {
                    var nameBytes = Encoding.UTF32.GetBytes(table.Key);

                    writer.Write(nameBytes.Length);
                    writer.Write(nameBytes);

                    table.Value.WriteSelf(writer);
                }
            });
        });
    }

    public DatabaseTable GetTable(string tableName)
    {
        if (!IsInitialized)
            throw new Exception("The database file has not been initialized yet");
        
        if (!_read)
            throw new Exception("The database file has not been loaded yet");
        
        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentNullException(nameof(tableName));
        
        if (_tables.TryGetValue(tableName, out var table))
            return table;

        table = new DatabaseTable(tableName, this);

        _tables.TryAdd(tableName, table);

        Monitor.Register(false);
        return table;
    }

    public bool DropTable(string tableName)
    {
        if (!IsInitialized)
            throw new Exception("The database file has not been initialized yet");
        
        if (!_read)
            throw new Exception("The database file has not been loaded yet");

        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentNullException(nameof(tableName));
        
        if (!_tables.TryRemove(tableName, out var table))
            return false;

        table.Dispose();

        Monitor.Register(false);
        return true;
    }

    private void AccessFile(Action action, bool ignoreRead = false)
    {
        if (!IsInitialized)
            throw new Exception("The database file has not been initialized yet");
        
        if (!_read && !ignoreRead)
            throw new Exception("The database file has not been loaded yet");

        _threadLock.Trigger();

        _fileLock.WaitBlocking(MaxLockTime, LockWaitTime, LockIgnoreThisProcess, LockMatchCustomPrefix, LockKillOverTime, LockDeleteOverTime, LockAllowSelfProcessKill);
        _fileLock.Trigger();

        var e = default(Exception);

        try
        {
            action();
        }
        catch (Exception ex)
        {
            e = ex;
        }
        finally
        {
            _fileLock.Release();
            _threadLock.Release();
        }

        if (e != null)
            throw e;
    }

    private T AccessFile<T>(Func<T> action, bool ignoreRead = false)
    {
        if (!IsInitialized)
            throw new Exception("The database file has not been initialized yet");
        
        if (!_read && !ignoreRead)
            throw new Exception("The database file has not been loaded yet");

        _threadLock.Trigger();

        _fileLock.WaitBlocking(MaxLockTime, LockWaitTime, LockIgnoreThisProcess, LockMatchCustomPrefix, LockKillOverTime, LockDeleteOverTime, LockAllowSelfProcessKill);
        _fileLock.Trigger();

        var e = default(Exception);
        var result = default(T);

        try
        {
            result = action();
        }
        catch (Exception ex)
        {
            e = ex;
        }
        finally
        {
            _fileLock.Release();
            _threadLock.Release();
        }

        if (e != null)
            throw e;

        return result;
    }
}