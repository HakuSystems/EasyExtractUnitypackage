using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EasyExtractCrossPlatform.Models;

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
    private static LoggingPreferences _preferences = LoggingPreferences.CreateDefault();
    private static readonly ConcurrentQueue<PendingLogEntry> PendingEntries = new();
    private static readonly SemaphoreSlim LogSignal = new(0);
    private static CancellationTokenSource? _logProcessingCts;
    private static Task? _logProcessingTask;
    private static bool _modeStateLogged;

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

    public static void ApplySettingsSnapshot(AppSettings settings, string? source = null)
    {
        if (settings is null)
            return;

        var preferences = LoggingPreferences.FromSettings(settings);
        ApplyPreferences(preferences, settings, source, !_modeStateLogged);
    }

    public static void LogInformation(string message)
    {
        WriteEntry("INFO", message, null);
    }

    public static void LogWarning(string message, Exception? exception = null)
    {
        WriteEntry("WARN", message, exception);
    }

    public static void LogError(string message, Exception? exception = null)
    {
        WriteEntry("ERROR", message, exception, exception is null);
    }

    public static void LogMode(string description)
    {
        WriteEntry("MODE", description, null);
    }

    public static void LogPerformance(string operation, TimeSpan duration, string? category = null,
        string? details = null, long? processedBytes = null)
    {
        var preferencesSnapshot = _preferences;
        if (!preferencesSnapshot.PerformanceLoggingEnabled)
            return;

        var builder = new StringBuilder()
            .Append(operation)
            .Append(" completed in ")
            .Append(duration.TotalMilliseconds.ToString("F2"))
            .Append(" ms");

        if (!string.IsNullOrWhiteSpace(category))
            builder.Append(" | category=").Append(category);

        if (!string.IsNullOrWhiteSpace(details))
            builder.Append(" | ").Append(details);

        if (processedBytes.HasValue)
            builder.Append(" | bytes=").Append(processedBytes.Value.ToString("N0"));

        if (preferencesSnapshot.MemoryTrackingEnabled)
        {
            var currentMemory = GC.GetTotalMemory(false);
            builder.Append(" | memory=").Append(FormatBytes(currentMemory));
        }

        WriteEntry("PERF", builder.ToString(), null);
    }

    public static IDisposable BeginPerformanceScope(string operation, string? category = null,
        string? correlationId = null)
    {
        var preferencesSnapshot = _preferences;
        if (!preferencesSnapshot.PerformanceLoggingEnabled)
            return NoopDisposable.Instance;

        return new PerformanceScope(operation, category, correlationId, preferencesSnapshot);
    }

    public static void LogMemoryUsage(string context, bool includeGcBreakdown = false)
    {
        var preferencesSnapshot = _preferences;
        if (!preferencesSnapshot.MemoryTrackingEnabled)
            return;

        var totalMemory = GC.GetTotalMemory(false);
        var builder = new StringBuilder()
            .Append(context)
            .Append(" | memory=")
            .Append(FormatBytes(totalMemory));

        if (includeGcBreakdown)
            builder.Append(" | gen0=").Append(GC.CollectionCount(0))
                .Append(" gen1=").Append(GC.CollectionCount(1))
                .Append(" gen2=").Append(GC.CollectionCount(2));

        WriteEntry("MEM", builder.ToString(), null);
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

    private static void WriteEntry(string level, string message, Exception? exception, bool includeCallerStack = false,
        bool bypassAsyncPipeline = false)
    {
        EnsureInitialized();

        var preferencesSnapshot = _preferences;
        string? capturedStack = null;

        if (includeCallerStack && exception is null && preferencesSnapshot.CaptureStackTraces)
            capturedStack = CaptureCallerStack();

        var entry = new PendingLogEntry(level, message, exception, capturedStack, preferencesSnapshot);

        if (!bypassAsyncPipeline && TryQueueEntry(entry))
            return;

        WriteEntryCore(entry);
    }

    private static bool TryQueueEntry(PendingLogEntry entry)
    {
        if (!entry.Preferences.AsyncLoggingEnabled)
            return false;

        EnsureAsyncProcessingLoop();

        PendingEntries.Enqueue(entry);
        try
        {
            LogSignal.Release();
        }
        catch (SemaphoreFullException)
        {
            if (PendingEntries.TryDequeue(out var overflowEntry))
                WriteEntryCore(overflowEntry);
        }

        return true;
    }

    private static void WriteEntryCore(PendingLogEntry entry)
    {
        var builder = new StringBuilder();
        builder.Append('[')
            .Append(entry.Level)
            .Append("] ")
            .Append(entry.Message);

        if (entry.Exception is not null)
        {
            builder.AppendLine();
            if (entry.Preferences.CaptureStackTraces)
                builder.Append(entry.Exception);
            else
                builder.Append(entry.Exception.GetType().FullName)
                    .Append(':')
                    .Append(' ')
                    .Append(entry.Exception.Message);
        }
        else if (!string.IsNullOrWhiteSpace(entry.StackTrace))
        {
            builder.AppendLine();
            builder.Append(entry.StackTrace);
        }

        var formattedPayload = builder.ToString();
        DispatchLogPayload(formattedPayload);

        if (string.Equals(entry.Level, "ERROR", StringComparison.OrdinalIgnoreCase))
            DiscordWebhookNotifier.PostErrorAsync(formattedPayload);
    }

    private static void DispatchLogPayload(string payload)
    {
        var wroteToCustomListener = false;

        if (_listener is { } listener)
            try
            {
                listener.WriteLine(payload);
                wroteToCustomListener = true;
            }
            catch (ObjectDisposedException)
            {
                // Listener was disposed between writes; fall through to Trace/Debugger.
            }

        if (!wroteToCustomListener)
            Trace.WriteLine(payload);

        if (Debugger.IsAttached)
            Debugger.Log(0, "EasyExtract", payload + Environment.NewLine);
    }

    private static void EnsureAsyncProcessingLoop()
    {
        if (_logProcessingTask is { IsCompleted: false })
            return;

        lock (SyncRoot)
        {
            if (_logProcessingTask is { IsCompleted: false })
                return;

            _logProcessingCts?.Dispose();
            _logProcessingCts = new CancellationTokenSource();
            _logProcessingTask = Task.Run(() => ProcessLogQueueAsync(_logProcessingCts.Token));
        }
    }

    private static void StopAsyncProcessingLoop(bool flushQueue)
    {
        CancellationTokenSource? cts;
        Task? worker;

        lock (SyncRoot)
        {
            if (_logProcessingTask is null)
            {
                if (flushQueue)
                    FlushPendingEntries();
                return;
            }

            cts = _logProcessingCts;
            worker = _logProcessingTask;
            _logProcessingTask = null;
            _logProcessingCts = null;
        }

        try
        {
            cts?.Cancel();
            LogSignal.Release();
            if (worker is not null)
                try
                {
                    var completed = worker.Wait(TimeSpan.FromSeconds(2));
                    if (!completed)
                        WriteEntry("WARN", "Logging background worker did not shut down within timeout.",
                            null, bypassAsyncPipeline: true);
                }
                catch (AggregateException ex) when (ex.InnerExceptions.All(static e => e is OperationCanceledException))
                {
                    // Expected if worker observes cancellation as fault.
                }
        }
        catch (AggregateException)
        {
            // Swallow cancellation aggregate from task shutdown.
        }
        catch (ObjectDisposedException)
        {
            // Semaphore already disposed during shutdown.
        }
        finally
        {
            cts?.Dispose();
        }

        if (flushQueue)
            FlushPendingEntries();
    }

    private static async Task ProcessLogQueueAsync(CancellationToken token)
    {
        try
        {
            while (true)
            {
                await LogSignal.WaitAsync(token).ConfigureAwait(false);
                FlushPendingEntries();
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown.
        }
        catch (Exception ex)
        {
            WriteEntry("ERROR", "Logging background pipeline faulted.", ex, bypassAsyncPipeline: true);
        }
        finally
        {
            FlushPendingEntries();
        }
    }

    private static void FlushPendingEntries()
    {
        while (PendingEntries.TryDequeue(out var entry))
            WriteEntryCore(entry);
    }

    private static string CaptureCallerStack()
    {
        try
        {
            var trace = new StackTrace(3, true);
            return trace.ToString();
        }
        catch
        {
            return Environment.StackTrace;
        }
    }

    private static string FormatBytes(long bytes)
    {
        var units = new[] { "B", "KB", "MB", "GB", "TB" };
        var value = (double)bytes;
        var unitIndex = 0;

        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return $"{value:0.##} {units[unitIndex]}";
    }

    private static void ApplyPreferences(LoggingPreferences preferences, AppSettings? settings, string? source,
        bool forceLog)
    {
        var startAsync = false;
        var stopAsync = false;

        lock (SyncRoot)
        {
            var changed = !_preferences.Equals(preferences);
            if (!changed && _modeStateLogged && !forceLog)
                return;

            startAsync = preferences.AsyncLoggingEnabled && !_preferences.AsyncLoggingEnabled;
            stopAsync = !preferences.AsyncLoggingEnabled && _preferences.AsyncLoggingEnabled;

            _preferences = preferences;
            _modeStateLogged = true;
        }

        if (startAsync)
            EnsureAsyncProcessingLoop();
        else if (stopAsync)
            StopAsyncProcessingLoop(true);

        var summary = BuildDiagnosticsSummary(preferences, settings, source);
        WriteEntry("MODE", summary, null, false,
            !preferences.AsyncLoggingEnabled);
    }

    private static string BuildDiagnosticsSummary(LoggingPreferences preferences, AppSettings? settings,
        string? source)
    {
        var builder = new StringBuilder()
            .Append("stackTrace=")
            .Append(preferences.CaptureStackTraces ? "on" : "off")
            .Append(" | perf=")
            .Append(preferences.PerformanceLoggingEnabled ? "on" : "off")
            .Append(" | memory=")
            .Append(preferences.MemoryTrackingEnabled ? "on" : "off")
            .Append(" | async=")
            .Append(preferences.AsyncLoggingEnabled ? "on" : "off");

        if (!string.IsNullOrWhiteSpace(source))
            builder.Append(" | source=").Append(source);

        if (settings is not null)
            builder.Append(" | license=")
                .Append(string.IsNullOrWhiteSpace(settings.LicenseTier) ? "Free" : settings.LicenseTier)
                .Append(" | uwuMode=")
                .Append(settings.UwUModeActive ? "on" : "off")
                .Append(" | discord=")
                .Append(settings.DiscordRpc ? "on" : "off");

        return builder.ToString();
    }

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

    private readonly record struct PendingLogEntry(
        string Level,
        string Message,
        Exception? Exception,
        string? StackTrace,
        LoggingPreferences Preferences);

    private readonly record struct LoggingPreferences(
        bool CaptureStackTraces,
        bool PerformanceLoggingEnabled,
        bool MemoryTrackingEnabled,
        bool AsyncLoggingEnabled)
    {
        public static LoggingPreferences CreateDefault()
        {
            return new LoggingPreferences(true, true, true, true);
        }

        public static LoggingPreferences FromSettings(AppSettings settings)
        {
            return new LoggingPreferences(settings.EnableStackTrace, settings.EnablePerformanceLogging,
                settings.EnableMemoryTracking, settings.EnableAsyncLogging);
        }
    }

    private sealed class PerformanceScope : IDisposable
    {
        private readonly string? _category;
        private readonly string? _correlationId;
        private readonly string _operation;
        private readonly LoggingPreferences _preferencesSnapshot;
        private readonly long? _startingMemory;
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private bool _disposed;

        public PerformanceScope(string operation, string? category, string? correlationId,
            LoggingPreferences preferencesSnapshot)
        {
            _operation = operation;
            _category = category;
            _correlationId = correlationId;
            _preferencesSnapshot = preferencesSnapshot;
            if (preferencesSnapshot.MemoryTrackingEnabled)
                _startingMemory = GC.GetTotalMemory(false);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _stopwatch.Stop();

            if (!_preferencesSnapshot.PerformanceLoggingEnabled)
                return;

            long? endingMemory = null;
            long? deltaMemory = null;

            if (_preferencesSnapshot.MemoryTrackingEnabled)
            {
                endingMemory = GC.GetTotalMemory(false);
                deltaMemory = endingMemory - (_startingMemory ?? endingMemory);
            }

            var builder = new StringBuilder()
                .Append(_operation)
                .Append(" took ")
                .Append(_stopwatch.Elapsed.TotalMilliseconds.ToString("F2"))
                .Append(" ms");

            if (!string.IsNullOrWhiteSpace(_category))
                builder.Append(" | category=").Append(_category);

            if (!string.IsNullOrWhiteSpace(_correlationId))
                builder.Append(" | correlation=").Append(_correlationId);

            if (endingMemory.HasValue)
            {
                builder.Append(" | memory=").Append(FormatBytes(endingMemory.Value));
                if (deltaMemory.HasValue)
                    builder.Append(" (+/-").Append(FormatBytes(deltaMemory.Value)).Append(')');
            }

            WriteEntry("PERF", builder.ToString(), null);
        }
    }

    private sealed class NoopDisposable : IDisposable
    {
        public static readonly NoopDisposable Instance = new();

        private NoopDisposable()
        {
        }

        public void Dispose()
        {
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