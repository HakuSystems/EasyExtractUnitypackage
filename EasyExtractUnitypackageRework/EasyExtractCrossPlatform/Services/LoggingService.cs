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
public static partial class LoggingService
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

}



