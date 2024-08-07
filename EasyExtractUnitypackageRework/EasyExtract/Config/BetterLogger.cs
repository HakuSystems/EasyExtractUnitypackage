using System.IO;

namespace EasyExtract.Config;

public class BetterLogger
{
    private readonly string _errorLogFile;
    private readonly string _infoLogFile;
    private readonly string _warningLogFile;

    public BetterLogger()
    {
        var applicationPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "EasyExtract", "Logs");

        Directory.CreateDirectory(applicationPath);

        _infoLogFile = Path.Combine(applicationPath, "InfoLog.txt");
        _warningLogFile = Path.Combine(applicationPath, "WarningLog.txt");
        _errorLogFile = Path.Combine(applicationPath, "ErrorLog.txt");

        // Clear the log files if they exist
        File.WriteAllText(_infoLogFile, string.Empty);
        File.WriteAllText(_warningLogFile, string.Empty);
        File.WriteAllText(_errorLogFile, string.Empty);
    }

    public Task LogAsync(string message, string source, Importance importance)
    {
        var logEntry = $"[{DateTime.Now}] [{importance}] {source}: {message}\n";
        Console.WriteLine(logEntry);

        var logFile = importance switch
        {
            Importance.Warning => _warningLogFile,
            Importance.Error => _errorLogFile,
            _ => _infoLogFile
        };

        return AsyncWrite(logFile, logEntry);
    }

    private async Task AsyncWrite(string logFile, string content)
    {
        await File.AppendAllTextAsync(logFile, content);
    }
}

public enum Importance
{
    Info,
    Warning,
    Error
}