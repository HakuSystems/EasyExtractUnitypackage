using System.Text;

namespace EasyExtractCrossPlatform.Services;

public static partial class LoggingService
{
    private static void EnsureInitialized()
    {
        if (_initialized)
            return;

        lock (SyncRoot)
        {
            if (_initialized)
                return;

            var logFileName = $"{LogFilePrefix}{DateTime.Now:yyyyMMdd}.log";
            var initializationNotes = new List<(string Level, string Message)>();
            TextWriter? writer = null;
            string? directoryInUse = null;
            string? logFilePath = null;
            string? lastAttemptedDirectory = null;

            foreach (var candidate in GetPreferredLogDirectories())
            {
                lastAttemptedDirectory = candidate;
                if (TryCreateLogWriter(candidate, logFileName, out writer, out logFilePath, out var failure))
                {
                    directoryInUse = candidate;
                    break;
                }

                initializationNotes.Add(("WARN",
                    $"Failed to initialize log directory '{candidate}': {failure}"));
            }

            if (writer is null)
            {
                writer = TextWriter.Synchronized(TextWriter.Null);
                directoryInUse = lastAttemptedDirectory ?? ResolveLogDirectory();
                logFilePath ??= Path.Combine(directoryInUse, logFileName);
                initializationNotes.Add(("ERROR",
                    "Logging disabled because no writable log directory was available. Entries will only appear in the debugger output."));
            }

            var listener = new TimestampedTraceListener(writer, "EasyExtractLogListener");

            Trace.AutoFlush = true;
            Trace.Listeners.Add(listener);

            _listener = listener;
            _logDirectory = directoryInUse;
            _logFilePath = logFilePath;

            WriteSessionHeader();
            if (Directory.Exists(directoryInUse))
                PruneOldLogs(directoryInUse);

            foreach (var (level, message) in initializationNotes)
                WriteEntryCore(new PendingLogEntry(level, message, null, null, _preferences));

            AppDomain.CurrentDomain.ProcessExit += (_, _) => Shutdown();
            AppDomain.CurrentDomain.DomainUnload += (_, _) => Shutdown();

            if (_preferences.AsyncLoggingEnabled)
                EnsureAsyncProcessingLoop();

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
                .Append("Process ")
                .Append(Environment.ProcessId)
                .Append(" | Path=")
                .Append(Environment.ProcessPath ?? "unknown")
                .AppendLine()
                .Append("OS: ")
                .Append(Environment.OSVersion)
                .Append(" | ")
                .Append(Environment.Is64BitProcess ? "x64" : "x86")
                .AppendLine()
                .Append("CLR ")
                .Append(Environment.Version)
                .AppendLine()
                .Append("Log file: ")
                .Append(_logFilePath)
                .AppendLine()
                .AppendLine(new string('=', 80))
                .ToString();

            DispatchLogPayload(header.TrimEnd());
        }
        catch (Exception ex)
        {
            WriteEntry("WARN", "Failed to write session header.", ex, bypassAsyncPipeline: true);
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
                    WriteEntryCore(new PendingLogEntry("WARN",
                        $"Failed to delete log file '{file}'.", ex, null, _preferences));
                }
        }
        catch (Exception ex)
        {
            WriteEntryCore(new PendingLogEntry("WARN",
                "Failed to prune old log files.", ex, null, _preferences));
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

    private static IEnumerable<string> GetPreferredLogDirectories()
    {
        var primary = ResolveLogDirectory();
        if (!string.IsNullOrWhiteSpace(primary))
            yield return primary;

        var tempDirectory = Path.Combine(Path.GetTempPath(), "EasyExtract", "logs");
        if (!string.IsNullOrWhiteSpace(tempDirectory) &&
            !string.Equals(tempDirectory, primary, StringComparison.OrdinalIgnoreCase))
            yield return tempDirectory;

        var processDirectory = AppContext.BaseDirectory ?? Environment.CurrentDirectory;
        if (!string.IsNullOrWhiteSpace(processDirectory))
        {
            var fallback = Path.Combine(processDirectory, "logs");
            if (!string.Equals(fallback, primary, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(fallback, tempDirectory, StringComparison.OrdinalIgnoreCase))
                yield return fallback;
        }
    }

    private static bool TryCreateLogWriter(string directory, string logFileName, out TextWriter? writer,
        out string? logFilePath, out string? failureMessage)
    {
        writer = null;
        logFilePath = null;
        failureMessage = null;

        try
        {
            Directory.CreateDirectory(directory);

            var filePath = Path.Combine(directory, logFileName);
            var stream = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.Write,
                FileShare.ReadWrite | FileShare.Delete);
            stream.Seek(0, SeekOrigin.End);
            writer = new StreamWriter(stream, new UTF8Encoding(false))
            {
                AutoFlush = true
            };

            logFilePath = filePath;
            return true;
        }
        catch (Exception ex)
        {
            failureMessage = $"{ex.GetType().Name}: {ex.Message}";
            return false;
        }
    }

    private static void Shutdown()
    {
        StopAsyncProcessingLoop(true);

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
}
