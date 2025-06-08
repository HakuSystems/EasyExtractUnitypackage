namespace EasyExtract.Utilities.Logger;

public class LogEntry
{
    public DateTime Timestamp { get; set; }
    public LogLevel Level { get; set; }
    public string Message { get; set; }
    public string Category { get; set; }
    public string CallerMethod { get; set; }
    public string CallerFile { get; set; }
    public int CallerLine { get; set; }
    public string ThreadId { get; set; }
    public long MemoryUsage { get; set; }
    public double ElapsedMs { get; set; }
    public Dictionary<string, object> Context { get; set; } = new();
    public Exception? Exception { get; set; }
    public StackTrace? StackTrace { get; set; }
}