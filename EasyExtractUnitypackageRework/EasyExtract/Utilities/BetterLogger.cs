using EasyExtract.Config;
using Serilog;

namespace EasyExtract.Utilities;

public class BetterLogger
{
    private static readonly object _lock = new();

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

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File(Path.Combine(logPath, "Log.txt"), LogEventLevel.Information,
                rollingInterval: RollingInterval.Day, shared: true)
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
        if (Directory.Exists(logPath))
            return;

        var logFiles = Directory.GetFiles(logPath);
        if (GetLogFileCreationDate(logFiles) < DateTime.Now.AddDays(-7))
            DeleteLogsFolder();
    }

    private static void DeleteLogsFolder()
    {
        var applicationPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "EasyExtract");
        var logPath = Path.Combine(applicationPath, "Logs");

        if (!Directory.Exists(logPath))
            Directory.CreateDirectory(logPath);

        var logFiles = Directory.GetFiles(logPath);
        foreach (var logFile in logFiles)
            if (FileNotBeingUsed(logFile))
                File.Delete(logFile);
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

    private static DateTime GetLogFileCreationDate(string[] logFiles)
    {
        if (logFiles.Length == 0)
            return DateTime.Now;
        var creationDates = logFiles.Select(logFile => File.GetCreationTime(logFile)).ToList();
        return creationDates.Min();
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