using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace EasyExtractCrossPlatform.Services;

/// <summary>
///     Provides persistent logging with Debug/Trace integration and helpers for accessing the log directory.
/// </summary>
public static class LoggingService
{
    private const string LogFilePrefix = "easyextract_";
    private const string LogFileExtension = ".log";
    private const int RetainedLogFileCount = 10;

    private static readonly object SyncRoot = new();

    private static bool _initialized;
    private static string? _logDirectory;
    private static string? _logFilePath;
    private static TimestampedTraceListener? _listener;

    public static string LogDirectory
    {
        get
        {
            EnsureInitialized();
            return _logDirectory!;
        }
    }

    public static string LogFilePath
    {
        get
        {
            EnsureInitialized();
            return _logFilePath!;
        }
    }

    public static void Initialize()
    {
        EnsureInitialized();
    }

    public static void LogInformation(string message)
    {
        WriteEntry("INFO", message, null);
    }

    public static void LogError(string message, Exception? exception = null)
    {
        WriteEntry("ERROR", message, exception);
    }

    public static bool TryOpenLogFolder()
    {
        try
        {
            EnsureInitialized();
            var directory = LogDirectory;

            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            WriteEntry("INFO", $"Opening log folder at '{directory}'.", null);

            ProcessStartInfo startInfo;

            if (OperatingSystem.IsWindows())
                startInfo = new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{directory}\"",
                    UseShellExecute = true
                };
            else if (OperatingSystem.IsMacOS())
                startInfo = new ProcessStartInfo
                {
                    FileName = "open",
                    Arguments = directory,
                    UseShellExecute = false
                };
            else
                startInfo = new ProcessStartInfo
                {
                    FileName = "xdg-open",
                    Arguments = directory,
                    UseShellExecute = false
                };

            Process.Start(startInfo);
            WriteEntry("INFO", "Log folder open command executed.", null);
            return true;
        }
        catch (Exception ex)
        {
            WriteEntry("ERROR", "Failed to open log folder.", ex);
            return false;
        }
    }

    private static void WriteEntry(string level, string message, Exception? exception)
    {
        EnsureInitialized();

        var builder = new StringBuilder();
        builder.Append('[')
            .Append(level)
            .Append("] ")
            .Append(message);

        if (exception is not null)
        {
            builder.AppendLine();
            builder.Append(exception);
        }

        Debug.WriteLine(builder.ToString());
    }

    private static void EnsureInitialized()
    {
        if (_initialized)
            return;

        lock (SyncRoot)
        {
            if (_initialized)
                return;

            var directory = ResolveLogDirectory();
            Directory.CreateDirectory(directory);

            var logFileName = $"{LogFilePrefix}{DateTime.Now:yyyyMMdd}.log";
            var logFilePath = Path.Combine(directory, logFileName);

            var stream = new FileStream(logFilePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
            var writer = new StreamWriter(stream, Encoding.UTF8)
            {
                AutoFlush = true
            };

            var listener = new TimestampedTraceListener(writer, "EasyExtractLogListener");

            Trace.AutoFlush = true;

            Trace.Listeners.Add(listener);

            _listener = listener;
            _logDirectory = directory;
            _logFilePath = logFilePath;

            WriteSessionHeader();
            PruneOldLogs(directory);

            AppDomain.CurrentDomain.ProcessExit += (_, _) => Shutdown();
            AppDomain.CurrentDomain.DomainUnload += (_, _) => Shutdown();

            _initialized = true;
        }
    }

    private static void WriteSessionHeader()
    {
        try
        {
            var header = new StringBuilder()
                .AppendLine()
                .AppendLine(new string('=', 80))
                .Append("EasyExtract session started at ")
                .Append(DateTimeOffset.Now.ToString("O"))
                .AppendLine()
                .AppendLine(new string('=', 80))
                .ToString();

            Debug.WriteLine(header.TrimEnd());
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to write session header: {ex}");
        }
    }

    private static void PruneOldLogs(string directory)
    {
        try
        {
            var logFiles = Directory
                .EnumerateFiles(directory, $"{LogFilePrefix}*{LogFileExtension}", SearchOption.TopDirectoryOnly)
                .Select(path =>
                {
                    try
                    {
                        var timestamp = File.GetCreationTimeUtc(path);
                        return new KeyValuePair<DateTime, string>(timestamp, path);
                    }
                    catch
                    {
                        return new KeyValuePair<DateTime, string>(DateTime.MinValue, path);
                    }
                })
                .OrderByDescending(pair => pair.Key)
                .Skip(RetainedLogFileCount)
                .Select(pair => pair.Value);

            foreach (var file in logFiles)
                try
                {
                    File.Delete(file);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to delete log file '{file}': {ex}");
                }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to prune old log files: {ex}");
        }
    }

    private static string ResolveLogDirectory()
    {
        if (OperatingSystem.IsWindows())
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrWhiteSpace(appData))
                appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            return Path.Combine(appData, "EasyExtract", "logs");
        }

        if (OperatingSystem.IsMacOS())
        {
            var appSupport = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrWhiteSpace(appSupport))
                appSupport = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Library",
                    "Application Support");

            return Path.Combine(appSupport, "EasyExtract", "logs");
        }

        var stateHome = Environment.GetEnvironmentVariable("XDG_STATE_HOME");
        if (!string.IsNullOrWhiteSpace(stateHome))
            return Path.Combine(stateHome, "EasyExtract", "logs");

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
            localAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        if (string.IsNullOrWhiteSpace(localAppData))
            localAppData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), ".local",
                "share");

        return Path.Combine(localAppData, "EasyExtract", "logs");
    }

    private static void Shutdown()
    {
        lock (SyncRoot)
        {
            if (_listener is null)
                return;

            try
            {
                Trace.Listeners.Remove(_listener);
                _listener.Flush();
                _listener.Close();
            }
            catch
            {
                // Ignored: shutdown best-effort.
            }

            _listener = null;
        }
    }

    private sealed class TimestampedTraceListener : TextWriterTraceListener
    {
        private readonly object _writeLock = new();

        public TimestampedTraceListener(TextWriter writer, string name)
            : base(writer, name)
        {
        }

        public override void Write(string? message)
        {
            WriteCore(message, false);
        }

        public override void WriteLine(string? message)
        {
            WriteCore(message, true);
        }

        private void WriteCore(string? message, bool appendNewLine)
        {
            lock (_writeLock)
            {
                if (Writer is not { } writer)
                    return;

                if (string.IsNullOrEmpty(message))
                {
                    if (appendNewLine)
                        writer.WriteLine();
                    writer.Flush();
                    NeedIndent = false;
                    return;
                }

                var timestamp = DateTimeOffset.Now.ToString("O");
                var normalized = message.ReplaceLineEndings("\n");
                var lines = normalized.Split('\n');

                for (var i = 0; i < lines.Length; i++)
                {
                    var line = lines[i];
                    var formatted = $"{timestamp} {line}";
                    var isLastLine = i == lines.Length - 1;

                    if (!isLastLine || appendNewLine)
                        writer.WriteLine(formatted);
                    else
                        writer.Write(formatted);
                }

                writer.Flush();
                NeedIndent = false;
            }
        }
    }
}