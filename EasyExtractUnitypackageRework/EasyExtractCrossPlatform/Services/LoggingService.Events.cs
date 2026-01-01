namespace EasyExtractCrossPlatform.Services;

public sealed class LogEntryEventArgs : EventArgs
{
    public LogEntryEventArgs(string level, string message, string formattedPayload, Exception? exception,
        DateTimeOffset timestamp)
    {
        Level = level;
        Message = message;
        FormattedPayload = formattedPayload;
        Exception = exception;
        Timestamp = timestamp;
    }

    public string Level { get; }

    public string Message { get; }

    public string FormattedPayload { get; }

    public Exception? Exception { get; }

    public DateTimeOffset Timestamp { get; }
}

public static partial class LoggingService
{
    public static event EventHandler<LogEntryEventArgs>? ErrorLogged;

    private static void NotifyErrorObservers(PendingLogEntry entry, string formattedPayload)
    {
        var handlers = ErrorLogged;
        if (handlers is null)
            return;

        var args = new LogEntryEventArgs(entry.Level, entry.Message, formattedPayload, entry.Exception,
            DateTimeOffset.Now);
        foreach (EventHandler<LogEntryEventArgs> handler in handlers.GetInvocationList())
            try
            {
                handler.Invoke(null, args);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[LoggingService] Error observer failed: {ex.Message}");
            }
    }
}