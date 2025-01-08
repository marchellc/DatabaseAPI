using System;
using System.Diagnostics;

using DatabaseAPI.Logging;

namespace DatabaseAPI;

public static class ProcessUtils
{
    private static volatile int _processId = -1;

    public static int ProcessId
    {
        get
        {
            if (_processId == -1)
                _processId = Process.GetCurrentProcess().Id;

            return _processId;
        }
    }

    public static bool ProcessExists(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return process is { HasExited: false, Responding: true };
        }
        catch
        {
            return false;
        }
    }

    public static bool TryKillProcess(int processId, bool allowSelfKill = false)
    {
        if (processId < 0)
            throw new ArgumentOutOfRangeException(nameof(processId));

        if (processId == ProcessId && !allowSelfKill)
        {
            Log.Debug("Process Utils", $"TryKillProcess() :: Attempted to kill own process (allowSelfKill = false)");
            return false;
        }

        var targetProcess = Process.GetProcessById(processId);

        if (targetProcess is null)
            return false;

        Log.Debug("Process Utils", $"TryKillProcess() :: Killing process ID '{targetProcess.Id}'");
        
        try
        {
            targetProcess.Kill();
            targetProcess.Dispose();
        }
        catch (Exception ex)
        {
            Log.Debug("Process Utils", $"TryKillProcess() :: Failed to kill process ID '{processId}':\n{ex}");
        }

        return true;
    }
}