using System.Text;

namespace EasyExtractCrossPlatform.Services;

public static partial class LoggingService
{
    private readonly record struct PendingLogEntry(
        string Level,
        string Message,
        Exception? Exception,
        string? StackTrace,
        LoggingPreferences Preferences,
        bool ForwardToWebhook);

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