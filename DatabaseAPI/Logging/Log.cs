using System;
using System.Collections.Concurrent;

namespace DatabaseAPI.Logging;

public static class Log
{
    private static volatile ConcurrentQueue<LogEntry> _logs = new ConcurrentQueue<LogEntry>();

    public static int LogSize => _logs.Count;

    public static void Info(string source, object message)
        => Print(source, message, null, LogLevel.Info);

    public static void Warn(string source, object message)
        => Print(source, message, null, LogLevel.Warn);

    public static void Debug(string source, object message)
        => Print(source, message, null, LogLevel.Debug);

    public static void Error(string source, object message)
        => Print(source, message, null, LogLevel.Error);

    public static void Error(string source, Exception exception)
        => Print(source, null, exception, LogLevel.Error);

    public static void Print(string source, object message, Exception exception, LogLevel level)
    {
        if (source is null && exception is null)
            throw new ArgumentNullException(nameof(source));

        if (message is null)
            throw new ArgumentNullException(nameof(message));
        
        string msg = string.Empty;

        if (message is null && exception != null)
            msg = exception.ToString();
        else if (message is string msgStr)
            msg = msgStr;
        else
            msg = message.ToString();

        _logs.Enqueue(new LogEntry(source, msg, exception, level));
    }

    public static bool Update(out LogEntry nextEntry)
        => _logs.TryDequeue(out nextEntry);
}