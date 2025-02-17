using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections.Concurrent;

using DatabaseAPI.IO.Interfaces;
using DatabaseAPI.IO.Locks;

using DatabaseAPI.IO.Serialization.Simple;
using DatabaseAPI.IO.Serialization.Text;

using DatabaseAPI.Logging;

namespace DatabaseAPI.IO;

public class DatabaseFile : IDisposable
{
    static DatabaseFile()
    {
        TryCollectReadersAndWriters(typeof(DatabaseFile).Assembly);
    }
    
    private static volatile ConcurrentDictionary<string, DatabaseFile> _files = new();
    
    private static volatile ConcurrentDictionary<Type, IObjectReaderWriter> readersWriters = new();
    private static volatile ConcurrentDictionary<Type, IObjectManipulator> manipulators = new();
    
    private static volatile ConcurrentDictionary<Type, IStreamDeserializer> deserializers = new();
    private static volatile ConcurrentDictionary<Type, IStreamSerializer> serializers = new();
    
    public static DatabaseFile GetOrCreate(string filePath, string lockName, string lockDirectory, string lockPrefix = null, 
        Action<DatabaseFile> onCreated = null, Action<DatabaseFile> onInitialized = null)
    {
        if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentNullException(nameof(filePath));
        if (string.IsNullOrWhiteSpace(lockName)) throw new ArgumentNullException(nameof(lockName));
        if (string.IsNullOrWhiteSpace(lockDirectory)) throw new ArgumentNullException(nameof(lockDirectory));

        filePath = Path.GetFullPath(filePath);
        lockDirectory = Path.GetFullPath(lockDirectory);

        if (_files.TryGetValue(filePath, out var dbFile)) return dbFile;

        dbFile = new DatabaseFile(filePath, lockName, lockDirectory, lockPrefix);
        
        onCreated?.Invoke(dbFile);
        
        dbFile.Initialize();
        
        onInitialized?.Invoke(dbFile);

        _files.TryAdd(filePath, dbFile);
        return dbFile;
    }
    
    public static bool TryGetReaderWriter(Type type, out IObjectReaderWriter readerWriter) => readersWriters.TryGetValue(type, out readerWriter);
    public static bool TryGetManipulator(Type type, out IObjectManipulator manipulator) => manipulators.TryGetValue(type, out manipulator);
    
    public static bool TryGetSerializer(Type type, out IStreamSerializer serializer) => serializers.TryGetValue(type, out serializer);
    public static bool TryGetDeserializer(Type type, out IStreamDeserializer deserializer) => deserializers.TryGetValue(type, out deserializer);

    public static bool TryRegisterReaderWriter(Type type, IObjectReaderWriter readerWriter)
    {
        if (type is null) throw new ArgumentNullException(nameof(type));
        if (readerWriter is null) throw new ArgumentNullException(nameof(readerWriter));
        
        return readersWriters.TryAdd(type, readerWriter);
    }

    public static bool TryRegisterManipulator(Type type, IObjectManipulator manipulator)
    {
        if (type is null) throw new ArgumentNullException(nameof(type));
        if (manipulator is null) throw new ArgumentNullException(nameof(manipulator));
        
        return manipulators.TryAdd(type, manipulator);
    }

    public static bool TryRegisterSerializer(IStreamSerializer serializer)
    {
        if (serializer is null) 
            throw new ArgumentNullException(nameof(serializer));
        
        return serializers.TryAdd(serializer.GetType(), serializer);
    }

    public static bool TryRegisterDeserializer(IStreamDeserializer deserializer)
    {
        if (deserializer is null)
            throw new ArgumentNullException(nameof(deserializer));
        
        return deserializers.TryAdd(deserializer.GetType(), deserializer);
    }

    public static int TryCollectReadersAndWriters(Assembly assembly)
    {
        if (assembly is null) throw new ArgumentNullException(nameof(assembly));

        var count = 0;

        foreach (var type in assembly.GetTypes())
        {
            if (type.IsGenericParameter || type.IsGenericType || type.IsGenericTypeDefinition ||
                type.IsConstructedGenericType) 
                continue;
            
            if (typeof(IObjectReaderWriter).IsAssignableFrom(type))
            {
                if (Activator.CreateInstance(type) is not IObjectReaderWriter readerWriter) continue;
                if (readerWriter.Type is null) continue;
                if (TryRegisterReaderWriter(readerWriter.Type, readerWriter)) count++;
            }
            else if (typeof(IObjectManipulator).IsAssignableFrom(type))
            {
                if (Activator.CreateInstance(type) is not IObjectManipulator manipulator) continue;
                if (manipulator.Type is null) continue;
                if (TryRegisterManipulator(manipulator.Type, manipulator)) count++;
            }
            else if (typeof(IStreamSerializer).IsAssignableFrom(type))
            {
                if (Activator.CreateInstance(type) is not IStreamSerializer serializer) continue;
                if (TryRegisterSerializer(serializer)) count++;
            }
            else if (typeof(IStreamDeserializer).IsAssignableFrom(type))
            {
                if (Activator.CreateInstance(type) is not IStreamDeserializer deserializer) continue;
                if (TryRegisterDeserializer(deserializer)) count++;
            }
        }

        return count;
    }

    private volatile int lockWaitMs = -1;
    private volatile int monitorIntervalMs = 10000;
    
    private volatile int arrayResizeFactor = 2;
    private volatile int arrayInitialSize = 64;

    private volatile int batchSize = 1000;

    private volatile bool lockIgnoreThis;
    private volatile bool lockMatchPrefix;
    private volatile bool lockKillOverTime;
    private volatile bool lockDeleteOverTime;
    private volatile bool lockAllowSelfKill;
    private volatile bool lockEnabled;

    private volatile bool read;
    
    private volatile string path;
    
    private volatile string lockName;
    private volatile string lockPrefix;
    private volatile string lockDirectory;
    
    private volatile IStreamDeserializer deserializer = TextStreamDeserializer.Instance;
    private volatile IStreamSerializer serializer = TextStreamSerializer.Instance;

    private volatile IFileReaderWriter fileReaderWriter = SimpleFileReaderWriter.Instance;
    private volatile ITableReaderWriter tableReaderWriter = SimpleTableReaderWriter.Instance;
    private volatile ICollectionReaderWriter collectionReaderWriter = SimpleCollectionReaderWriter.Instance;
    
    private volatile DatabaseMonitor monitor;
    private volatile StatusLock threadLock;
    private volatile FileLock fileLock;

    private volatile ConcurrentDictionary<string, DatabaseTable> tables = new ConcurrentDictionary<string, DatabaseTable>();

    public string FilePath => path;
    
    public string LockName => lockName;
    public string LockPrefix => lockPrefix;
    public string LockDirectory => lockDirectory;
    
    public TimeSpan MaxLockTime { get; set; } = TimeSpan.Zero;

    public int BatchSize
    {
        get => batchSize;
        set => batchSize = value;
    }

    public int ArrayInitialSize
    {
        get => arrayInitialSize;
        set
        {
            if (value < 1) 
                throw new ArgumentOutOfRangeException(nameof(value));
            
            arrayInitialSize = value;
        }
    }

    public int ArrayResizeMultiplier
    {
        get => arrayResizeFactor;
        set
        {
            if (value < 1) 
                throw new ArgumentOutOfRangeException(nameof(value));
            
            arrayResizeFactor = value;
        }
    }
    
    public int MonitorCheckInterval
    {
        get => monitorIntervalMs;
        set => monitorIntervalMs = value;
    }
    
    public int LockWaitTime
    {
        get => lockWaitMs;
        set => lockWaitMs = value;
    }

    public bool LockIgnoreThisProcess
    {
        get => lockIgnoreThis;
        set => lockIgnoreThis = value;
    }

    public bool LockMatchCustomPrefix
    {
        get => lockMatchPrefix;
        set => lockMatchPrefix = value;
    }

    public bool LockKillOverTime
    {
        get => lockKillOverTime;
        set => lockKillOverTime = value;
    }

    public bool LockDeleteOverTime
    {
        get => lockDeleteOverTime;
        set => lockDeleteOverTime = value;
    }

    public bool LockAllowSelfProcessKill
    {
        get => lockAllowSelfKill;
        set => lockAllowSelfKill = value;
    }

    public bool LockEnabled
    {
        get => lockEnabled;
        set => lockEnabled = value;
    }

    public IStreamDeserializer Deserializer
    {
        get => deserializer;
        set => deserializer = value;
    }

    public IStreamSerializer Serializer
    {
        get => serializer;
        set => serializer = value;
    }
    
    public IFileReaderWriter FileReaderWriter
    {
        get => fileReaderWriter;
        set => fileReaderWriter = value;
    }

    public ITableReaderWriter TableReaderWriter
    {
        get => tableReaderWriter;
        set => tableReaderWriter = value;
    }

    public ICollectionReaderWriter CollectionReaderWriter
    {
        get => collectionReaderWriter;
        set => collectionReaderWriter = value;
    }

    public bool IsInitialized => threadLock != null && fileLock != null;
    public bool IsRead => read;

    public DatabaseMonitor Monitor => monitor;
    public ConcurrentDictionary<string, DatabaseTable> Tables => tables;
    
    private DatabaseFile(string filePath, string lockName, string lockDirectory, string lockPrefix = null)
    {
        path = filePath;
        
        this.lockName = lockName;
        this.lockPrefix = lockPrefix;
        this.lockDirectory = lockDirectory;
    }

    public void Initialize()
    {
        if (threadLock != null && fileLock != null && monitor != null)
        {
            Log.Debug("Database File", "This file is already initialized.");
            return;
        }
        
        if (string.IsNullOrWhiteSpace(FilePath)) throw new ArgumentNullException(nameof(FilePath));
        if (string.IsNullOrWhiteSpace(LockName)) throw new ArgumentNullException(nameof(LockName));
        if (string.IsNullOrWhiteSpace(LockDirectory)) throw new ArgumentNullException(nameof(LockDirectory));

        try
        {
            threadLock = new StatusLock();

            if (!Directory.Exists(LockDirectory)) Directory.CreateDirectory(LockDirectory);

            fileLock = FileLock.GetOrCreate(LockName, LockDirectory, LockPrefix);

            monitor = new DatabaseMonitor(path);

            monitor.OnReadAsync += ReadAsync;
            monitor.OnWriteAsync += WriteAsync;

            monitor.Start(monitorIntervalMs);

            if (!_files.ContainsKey(path)) _files.TryAdd(path, this);
        }
        catch (Exception ex)
        {
            Log.Error("Database File", $"Exception during initialization:\n{ex}");
        }
    }

    public void Dispose()
    {
        _files.TryRemove(path, out _);

        foreach (var table in tables) table.Value.Dispose();

        tables.Clear(); 
        tables = null;

        if (monitor != null)
        {
            monitor.OnReadAsync -= ReadAsync;
            monitor.OnWriteAsync -= WriteAsync;
            
            monitor.Dispose();
            monitor = null;
        }
        
        if (fileLock != null)
        {
            if (fileLock.IsLocked) fileLock.Release();

            fileLock.Dispose();
            fileLock = null;
        }

        if (threadLock != null)
        {
            if (threadLock.IsLocked) threadLock.Release();

            threadLock.Dispose();
            threadLock = null;
        }

        read = false;
    }

    public async Task ReadAsync()
    {
        await FileUtils.ReadFileAsync(path, async reader =>
        {
            await fileReaderWriter.ReadAsync(reader, this, deserializer, !read);
        });
        
        read = true;
    }

    public async Task WriteAsync()
    {
        await FileUtils.WriteFileAsync(path, async writer =>
        {
            await fileReaderWriter.WriteAsync(writer, this, serializer);
        });
    }

    public DatabaseTable InstantiateTable(string tableName)
    {
        if (!IsInitialized) throw new Exception("The database file has not been initialized yet");
        if (string.IsNullOrWhiteSpace(tableName)) throw new ArgumentNullException(nameof(tableName));
        
        return new DatabaseTable(tableName, this);
    }

    public DatabaseTable GetTable(string tableName)
    {
        if (!IsInitialized) throw new Exception("The database file has not been initialized yet");
        if (!read) throw new Exception("The database file has not been loaded yet");
        if (string.IsNullOrWhiteSpace(tableName)) throw new ArgumentNullException(nameof(tableName));
        
        if (tables.TryGetValue(tableName, out var table)) return table;

        table = new DatabaseTable(tableName, this);

        tables.TryAdd(tableName, table);
        return table;
    }

    public bool DropTable(string tableName)
    {
        if (!IsInitialized) throw new Exception("The database file has not been initialized yet");
        if (!read) throw new Exception("The database file has not been loaded yet");
        if (string.IsNullOrWhiteSpace(tableName)) throw new ArgumentNullException(nameof(tableName));
        
        if (!tables.TryRemove(tableName, out var table)) return false;

        table.Dispose();

        Monitor.Register();
        return true;
    }

    private async Task AccessFile(Action action)
    {
        if (!IsInitialized) throw new Exception("The database file has not been initialized yet");

        threadLock.Trigger();

        if (lockEnabled)
        {
            await fileLock.WaitAsync(MaxLockTime, LockWaitTime, LockIgnoreThisProcess, LockMatchCustomPrefix, LockKillOverTime, LockDeleteOverTime, LockAllowSelfProcessKill);
            await fileLock.TriggerAsync();
        }

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
            fileLock.Release();
            threadLock.Release();
        }

        if (e != null) throw e;
    }

    private async Task<T> AccessFile<T>(Func<Task<T>> action)
    {
        if (!IsInitialized) throw new Exception("The database file has not been initialized yet");

        threadLock.Trigger();

        if (lockEnabled)
        {
            await fileLock.WaitAsync(MaxLockTime, LockWaitTime, LockIgnoreThisProcess, LockMatchCustomPrefix, LockKillOverTime, LockDeleteOverTime, LockAllowSelfProcessKill);
            await fileLock.TriggerAsync();
        }

        var e = default(Exception);
        var result = default(T);

        try
        {
            result = await action();
        }
        catch (Exception ex)
        {
            e = ex;
        }
        finally
        {
            fileLock.Release();
            threadLock.Release();
        }

        if (e != null) throw e;
        return result;
    }
}