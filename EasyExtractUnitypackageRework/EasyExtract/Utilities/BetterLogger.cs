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

    private void InitializeLogger()
    {
        DeleteLogsFolder();
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
    }

    private void DeleteLogsFolder()
    {
        var logsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EasyExtract",
            "Logs");

        if (Directory.Exists(logsPath))
            try
            {
                var files = Directory.GetFiles(logsPath);
                foreach (var file in files)
                    try
                    {
                        File.Delete(file);
                    }
                    catch (IOException ex)
                    {
                        Console.WriteLine($"Failed to delete file {file}: {ex.Message}");
                    }

                try
                {
                    Directory.Delete(logsPath, true);
                }
                catch (IOException ex)
                {
                    Console.WriteLine($"Failed to delete logs folder: {ex.Message}");
                }
            }
            catch (DirectoryNotFoundException)
            {
                // Ignore if the directory is not found
            }
            catch (IOException ex)
            {
                Console.WriteLine($"Failed to delete logs folder: {ex.Message}");
            }
    }

    public Task LogAsync(string message, string source, Importance importance)
    {
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