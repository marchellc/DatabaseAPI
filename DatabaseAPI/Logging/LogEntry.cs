using System;

namespace DatabaseAPI.Logging;

public struct LogEntry
{
    public readonly string Source;
    public readonly string Message;

    public readonly Exception Exception;
    public readonly LogLevel Level;

    public readonly DateTime Time;

    public LogEntry(string source, string message, Exception exception, LogLevel level)
    {
        Level = level;
        Source = source;
        Message = message;
        Exception = exception;

        Time = DateTime.Now;
    }
}