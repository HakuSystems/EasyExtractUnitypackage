using EasyExtract.Models;
using Serilog;

namespace EasyExtract.Utilities;

public class BetterLogger
{
    private static readonly object _lock = new();
    private static readonly string SessionStartTime = DateTime.Now.ToString("yyyyMMdd_HHmmss");

    public BetterLogger()
    {
        InitializeLogger();
    }

    private static void InitializeLogger()
    {
        var applicationPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "EasyExtract");
        var logPath = Path.Combine(applicationPath, "Logs");

        Directory.CreateDirectory(logPath);

        var logFileName = $"Log_{SessionStartTime}.txt";

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File(Path.Combine(logPath, logFileName), LogEventLevel.Information,
                rollingInterval: RollingInterval.Infinite, shared: true)
            .CreateLogger();

        DeleteLogsFolderIfRequired();
    }

    private static void DeleteLogsFolderIfRequired()
    {
        var applicationPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "EasyExtract");
        var logPath = Path.Combine(applicationPath, "Logs");

        if (!Directory.Exists(logPath))
            Directory.CreateDirectory(logPath);

        var logFiles = Directory.GetFiles(logPath);

        var currentLogFile = Path.Combine(logPath, $"Log_{SessionStartTime}.txt");
        var filesToDelete = logFiles.Where(f => f != currentLogFile).ToArray();

        foreach (var file in filesToDelete)
            if (FileNotBeingUsed(file))
                File.Delete(file);
    }

    private static bool FileNotBeingUsed(string logFile)
    {
        try
        {
            using var stream = new FileStream(logFile, FileMode.Open, FileAccess.Read, FileShare.None);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }

    public static Task LogAsync(string message, string source, Importance importance)
    {
        InitializeLogger();
        var logEntry = $"[{importance}] {source}: {message}";

        lock (_lock)
        {
            switch (importance)
            {
                case Importance.Info:
                    Log.Information(logEntry);
                    break;
                case Importance.Warning:
                    Log.Warning(logEntry);
                    break;
                case Importance.Error:
                    Log.Error(logEntry);
                    break;
                case Importance.Debug:
                    Log.Debug(logEntry);
                    break;
            }
        }

        return Task.CompletedTask;
    }

    ~BetterLogger()
    {
        Log.CloseAndFlush();
    }
}