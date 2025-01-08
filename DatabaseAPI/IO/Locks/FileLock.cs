using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;

using DatabaseAPI.Logging;

namespace DatabaseAPI.IO.Locks;

public class FileLock : IDisposable
{
    private static volatile char[] _lockSeparator = new char[] { '_' };
    private static volatile ConcurrentDictionary<string, FileLock> _lockCache = new ConcurrentDictionary<string, FileLock>();

    public static char[] LockSeparator => _lockSeparator;
    public static ConcurrentDictionary<string, FileLock> Locks => _lockCache;

    public static FileLock GetOrCreate(string fileName, string fileDirectory, string customPrefix = null)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentNullException(nameof(fileName));

        if (string.IsNullOrWhiteSpace(fileDirectory))
            throw new ArgumentNullException(nameof(fileDirectory));

        fileDirectory = Path.GetFullPath(fileDirectory);

        var fullPath = Path.Combine(fileDirectory, fileName);
        var hasPrefix = !string.IsNullOrWhiteSpace(customPrefix);

        foreach (var activeLock in _lockCache)
        {
            if (activeLock.Value.Directory is null)
                continue;
            
            if (activeLock.Key != fullPath)
                continue;
            
            if (hasPrefix && (string.IsNullOrWhiteSpace(activeLock.Value.Prefix) || activeLock.Value.Prefix != customPrefix))
                continue;

            return activeLock.Value;
        }

        var fileLock = new FileLock(fileName, fileDirectory, fullPath, customPrefix);

        Locks.TryAdd(fullPath, fileLock);
        return fileLock;
    }

    private volatile bool _isLocked;
    private volatile string _lastLockPath;

    public volatile string Name;
    public volatile string Prefix;
    public volatile string FullPath;

    public volatile DirectoryInfo Directory;
    
    public bool IsLocked => _isLocked;

    private FileLock(string fileName, string fileDirectory, string fullPath, string customPrefix = null)
    {
        Name = fileName;
        Prefix = customPrefix;
        FullPath = fullPath;
        
        Directory = new DirectoryInfo(fileDirectory);
    }

    public void Trigger()
    {
        while (_isLocked)
            continue;
        
        _isLocked = true;
        _lastLockPath = Path.Combine(Directory.FullName, ToLockName(ProcessUtils.ProcessId, DateTime.Now.Ticks, Name, Prefix));

        FileUtils.TryCreate(_lastLockPath);
    }

    public void Release()
    {
        if (!_isLocked)
            return;

        FileUtils.TryDelete(_lastLockPath);

        _isLocked = false;
    }
    
    public void WaitNonBlocking(
        Action onReleased,
        
        TimeSpan maxLockTime, 
        int waitTimeMs = 200, 
        
        bool ignoreThisProcess = false, 
        bool matchCustomPrefix = false, 
        bool killOverTime = false, 
        bool deleteOverTime = true, 
        bool allowSelfKill = false)
    {
        if (onReleased is null)
            throw new ArgumentNullException(nameof(onReleased));

        var scheduler = ThreadUtils.Scheduler;
        
        Task.Run(async () =>
        {
            while (IsFileLocked(maxLockTime, ignoreThisProcess, matchCustomPrefix, killOverTime, deleteOverTime, allowSelfKill,
                       out var holderId, out var holderTime, out _))
            {
                if (waitTimeMs > 0)
                {
                    Log.Debug("File Lock", $"Wait() :: File is locked by process ID {holderId} (lock created at {holderTime})");
                    await Task.Delay(waitTimeMs);
                }
            }
        }).ContinueWith(_ => onReleased(), scheduler);
    }

    public void WaitBlocking(
        TimeSpan maxLockTime, 
        int waitTimeMs = 200, 
        
        bool ignoreThisProcess = false, 
        bool matchCustomPrefix = false, 
        bool killOverTime = false, 
        bool deleteOverTime = true, 
        bool allowSelfKill = false)
    {
        while (IsFileLocked(maxLockTime, ignoreThisProcess, matchCustomPrefix, killOverTime, deleteOverTime, allowSelfKill, 
                   out var holderId, out var holderTime, out _))
        {
            if (waitTimeMs > 0)
            {
                Log.Debug("File Lock", $"Wait() :: File is locked by process ID {holderId} (lock created at {holderTime})");
                Thread.Sleep(waitTimeMs);
            }
        }
    }
    
    public async Task WaitAsync(
        TimeSpan maxLockTime, 
        int waitTimeMs = 200, 
        
        bool ignoreThisProcess = false, 
        bool matchCustomPrefix = false, 
        bool killOverTime = false, 
        bool deleteOverTime = true, 
        bool allowSelfKill = false)
    {
        while (IsFileLocked(maxLockTime, ignoreThisProcess, matchCustomPrefix, killOverTime, deleteOverTime, allowSelfKill, 
                   out var holderId, out var holderTime, out _))
        {
            if (waitTimeMs > 0)
            {
                Log.Debug("File Lock", $"Wait() :: File is locked by process ID {holderId} (lock created at {holderTime})");
                await Task.Delay(waitTimeMs);
            }
        }
    }

    public void Dispose()
    {
        if (!string.IsNullOrWhiteSpace(FullPath))
            Locks.TryRemove(FullPath, out _);

        Name = null;
        FullPath = null;
        Directory = null;
    }

    private bool IsFileLocked(TimeSpan maxLockTime, bool ignoreThisProcess, bool matchCustomPrefix, bool killOverTime, bool deleteOverTime, bool allowSelfKill, out int holderId, out DateTime holderTime, out FileInfo holderFile)
    {
        foreach (var file in Directory.EnumerateFiles())
        {
            Log.Debug("File Lock", $"IsFileLocked() :: Reading file {file.Name}");

            if (!TryReadLockName(file.Name, out holderId, out holderTime, out var lockName, out var customPrefix))
            {
                Log.Debug("File Lock", $"IsFileLocked() :: Failed to read lock, deleting");

                FileUtils.TryDelete(file.FullName);
                continue;
            }

            if (lockName != Name)
            {
                Log.Debug("File Lock", $"IsFileLocked() :: Could not match lock name, ignoring ..");
                continue;
            }

            if (!string.IsNullOrWhiteSpace(Prefix) && matchCustomPrefix &&
                (string.IsNullOrWhiteSpace(customPrefix) || customPrefix != Prefix))
            {
                Log.Debug("File Lock", $"IsFileLocked() :: Could not match custom prefix, ignoring");
                continue;
            }

            if (ignoreThisProcess && holderId == ProcessUtils.ProcessId)
            {
                Log.Debug("File Lock", $"IsFileLocked() :: Lock belongs to this process, ignoring");
                continue;
            }

            if (maxLockTime > TimeSpan.Zero && (DateTime.Now - holderTime) > maxLockTime)
            {
                if (killOverTime)
                {
                    Log.Debug("File Lock", $"IsFileLocked() :: Lock is expired, killing process");
                    ProcessUtils.TryKillProcess(holderId, allowSelfKill);
                }

                if (deleteOverTime)
                {
                    Log.Debug("File Lock", $"IsFileLocked() :: Lock is expired, deleting file");
                    FileUtils.TryDelete(file.FullName);
                }

                continue;
            }

            if (!ProcessUtils.ProcessExists(holderId))
            {
                Log.Debug("File Lock", $"IsFileLocked() :: Process '{holderId}' no longer exists, deleting");

                FileUtils.TryDelete(file.FullName);
                continue;
            }

            holderFile = file;
            return true;
        }
        
        Log.Debug("File Lock", $"IsFileLocked() :: File is not locked");

        holderId = -1;
        holderFile = null;
        holderTime = default;

        return false;
    }

    public static string ToLockName(int processId, long lockTime, string lockName, string customPrefix = null)
    {
        if (string.IsNullOrWhiteSpace(lockName))
            throw new ArgumentNullException(nameof(lockName));

        if (string.IsNullOrWhiteSpace(customPrefix))
            return string.Format("{0}_{1}_{2}", processId, lockTime, lockName);
        else
            return string.Format("{0}_{1}_{2}_{3}", customPrefix, processId, lockTime, lockName);
    }

    public static bool TryReadLockName(string lockFileName, out int lockProcessId, out DateTime lockTime, out string lockName, out string customPrefix)
    {
        if (string.IsNullOrWhiteSpace(lockFileName))
            throw new ArgumentNullException(nameof(lockFileName));

        lockProcessId = -1;
        lockName = null;
        lockTime = default;

        customPrefix = null;

        var parts = lockFileName.Split(LockSeparator);

        if (parts.Length < 3)
            return false;

        if (parts.Length == 4)
            customPrefix = parts[0];

        var processIdPart = parts[parts.Length - 3];
        var timePart = parts[parts.Length - 2];
        var namePart = parts[parts.Length - 1];

        if (!int.TryParse(processIdPart, out lockProcessId))
            return false;

        if (!long.TryParse(timePart, out var timeTicks))
            return false;

        if (string.IsNullOrWhiteSpace(namePart))
            return false;

        lockTime = new DateTime(timeTicks);
        lockName = namePart;
        
        return true;
    }
}