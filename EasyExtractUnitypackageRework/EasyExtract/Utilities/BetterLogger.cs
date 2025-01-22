using EasyExtract.Config.Models;
using Serilog;

namespace EasyExtract.Utilities;

/// <summary>
///     A static utility for handling Serilog-based logging in both console and file outputs.
///     Handles log file organization, color-coding of log messages, and contextual stack trace info.
/// </summary>
public static class BetterLogger
{
    private static readonly object _lock = new();
    private static readonly string _sessionStartTime = DateTime.Now.ToString("yyyyMMdd_HHmmss");
    private static readonly string _applicationPath;
    private static readonly string _logPath;
    private static readonly string _currentLogFile;

    /// <summary>
    ///     Initializes static read-only fields, configures the Serilog logger,
    ///     and cleans up old log files in the log directory.
    /// </summary>
    static BetterLogger()
    {
        _applicationPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EasyExtract");

        _logPath = Path.Combine(_applicationPath, "Logs");
        Directory.CreateDirectory(_logPath);

        _currentLogFile = Path.Combine(_logPath, $"Log_{_sessionStartTime}.txt");

        ConfigureLogger();
        CleanupOldLogFiles();
    }

    /// <summary>
    ///     Configures Serilog to write to both the console (color-enabled) and
    ///     a single log file for the current session. Log level is set to Debug.
    /// </summary>
    private static void ConfigureLogger()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console() // Console logging (with colors)
            .WriteTo.File(
                _currentLogFile,
                LogEventLevel.Information,
                rollingInterval: RollingInterval.Infinite,
                shared: true)
            .CreateLogger();
    }

    /// <summary>
    ///     Deletes old log files from the log directory, leaving only the current session’s log file.
    ///     Any file that is locked or cannot be accessed is skipped.
    /// </summary>
    private static void CleanupOldLogFiles()
    {
        if (!Directory.Exists(_logPath))
            Directory.CreateDirectory(_logPath);

        var logFiles = Directory.GetFiles(_logPath);
        var filesToDelete = logFiles
            .Where(f => !f.Equals(_currentLogFile, StringComparison.OrdinalIgnoreCase));

        foreach (var file in filesToDelete)
            if (FileNotBeingUsed(file))
                try
                {
                    File.Delete(file);
                }
                catch (Exception ex)
                {
                    Log.Warning("Could not delete old log file {FileName}. Reason: {Reason}", file, ex.Message);
                }
    }

    /// <summary>
    ///     Attempts to open a file in exclusive read mode. Returns <c>true</c> if successful,
    ///     indicating the file is not locked. Returns <c>false</c> if an <see cref="IOException" /> is thrown.
    /// </summary>
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

    /// <summary>
    ///     Retrieves the nearest stack frame outside of <c>BetterLogger</c>, returning
    ///     the short filename (e.g. <c>Program.cs</c>) and line number where <c>LogAsync</c> was invoked.
    ///     Falls back to a default string if no frames are found.
    /// </summary>
    private static string GetCallSiteInfo()
    {
        var stack = new StackTrace(1, true); // skip 1 frame to ignore this method
        var frames = stack.GetFrames();

        // If no frames are available, return fallback info.
        if (frames == null || frames.Length == 0)
            return "UnknownFile at line 0";

        foreach (var frame in frames)
        {
            var method = frame.GetMethod();
            var declaringType = method?.DeclaringType?.FullName ?? string.Empty;

            // Skip frames from 'BetterLogger' so we can show the actual caller
            if (!declaringType.Contains("EasyExtract.Utilities.BetterLogger"))
            {
                var fileName = Path.GetFileName(frame.GetFileName()) ?? "UnknownFile";
                var lineNumber = frame.GetFileLineNumber();
                return $"{fileName} at line {lineNumber}";
            }
        }

        return "UnknownFile at line 0";
    }

    /// <summary>
    ///     If an exception is provided, returns the first line of its stack trace.
    ///     Otherwise, returns the caller file and line info from <c>GetCallSiteInfo()</c>.
    /// </summary>
    private static string GetStackTraceLineOrCaller(Exception? ex)
    {
        if (ex != null)
        {
            var firstLine = ex.StackTrace?.Split('\n').FirstOrDefault();
            return string.IsNullOrWhiteSpace(firstLine)
                ? "No stack trace available"
                : firstLine;
        }

        // Use the caller file + line number
        return $"CallSite: {GetCallSiteInfo()}";
    }

    /// <summary>
    ///     Applies ANSI color codes to the log entry, highlighting level, stack trace, and message.
    ///     Returns the combined colorized string.
    /// </summary>
    private static string ColorizeLogEntry(Importance importance, string stackTrace, string message)
    {
        // Map importance levels to ANSI color codes
        var importanceColor = importance switch
        {
            Importance.Info => "36", // cyan
            Importance.Warning => "33", // yellow
            Importance.Error => "31", // red
            Importance.Debug => "90", // bright black
            _ => "37" // white
        };

        // Color codes for stack trace & message
        const string stackTraceColor = "35"; // magenta
        const string messageColor = "37"; // white

        static string WrapColor(string text, string colorCode)
        {
            return $"\x1b[{colorCode}m{text}\x1b[0m";
        }

        // Example final format:
        // "[Info]" / "[CallSite: Program.cs at line 42]" The actual message
        return
            $"{WrapColor($"[{importance}]", importanceColor)} / " +
            $"{WrapColor($"[{stackTrace}]", stackTraceColor)} " +
            $"{WrapColor(message, messageColor)}";
    }

    /// <summary>
    ///     Logs the given <paramref name="message" /> asynchronously with the specified
    ///     <paramref name="importance" /> level. If an <see cref="Exception" /> is provided,
    ///     includes the first line of its stack trace in the log. Color codes are written
    ///     to both console and file.
    /// </summary>
    public static Task LogAsync(
        string message,
        Importance importance,
        Exception? ex = null)
    {
        var stackTraceInfo = GetStackTraceLineOrCaller(ex);
        var logEntryColored = ColorizeLogEntry(importance, stackTraceInfo, message);

        lock (_lock)
        {
            switch (importance)
            {
                case Importance.Info:
                    Log.Information(logEntryColored);
                    break;
                case Importance.Warning:
                    Log.Warning(logEntryColored);
                    break;
                case Importance.Error:
                    Log.Error(logEntryColored);
                    break;
                case Importance.Debug:
                    Log.Debug(logEntryColored);
                    break;
            }
        }

        return Task.CompletedTask;
    }
}