using System.IO;
using Serilog;
using Serilog.Events;

namespace EasyExtract.Config;

public class BetterLogger
{
    private static readonly object _lock = new();

    public BetterLogger()
    {
        DeleteLogsFolder();
        var applicationPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "EasyExtract");
        var logPath = Path.Combine(applicationPath, "Logs");

        if (!Directory.Exists(logPath))
            Directory.CreateDirectory(logPath);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File(Path.Combine(logPath, "Log.txt"), LogEventLevel.Information,
                rollingInterval: RollingInterval.Infinite, shared: true)
            .CreateLogger();
    }

    private void DeleteLogsFolder()
    {
        var logsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EasyExtract",
            "Logs");

        if (Directory.Exists(logsPath))
            try
            {
                foreach (var file in Directory.GetFiles(logsPath))
                    try
                    {
                        using (var fileStream = new FileStream(file, FileMode.Open, FileAccess.ReadWrite))
                        {
                            fileStream.Close();
                        }

                        File.Delete(file);
                    }
                    catch (IOException ex)
                    {
                        Console.WriteLine($"Failed to delete file {file}: {ex.Message}");
                    }

                Directory.Delete(logsPath, true);
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