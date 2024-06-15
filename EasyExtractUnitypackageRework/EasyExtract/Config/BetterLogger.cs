using System.IO;

namespace EasyExtract.Config;

public class BetterLogger
{
    private static readonly object _lock = new();
    private readonly string _logFile;

    public BetterLogger()
    {
        var applicationPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "EasyExtract");
        var logPath = Path.Combine(applicationPath, "Logs");
        if (!Directory.Exists(logPath))
            Directory.CreateDirectory(logPath);
        _logFile = Path.Combine(logPath, "Log.txt");

        // Clear the log file if it exists
        lock (_lock)
        {
            File.WriteAllText(_logFile, string.Empty);
        }
    }

    public async Task LogAsync(string message, string source, Importance importance)
    {
        var logEntry = $"[{DateTime.Now}] [{importance}] {source}: {message}\n";

        lock (_lock)
        {
            File.AppendAllText(_logFile, logEntry);
        }
    }
}

public enum Importance
{
    Info,
    Warning,
    Error
}