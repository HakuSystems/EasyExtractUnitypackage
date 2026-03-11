using EasyExtract.Core;

namespace EasyExtractCrossPlatform.Tests.Support;

internal sealed class RecordingLogger : IEasyExtractLogger
{
    public List<string> InformationMessages { get; } = new();
    public List<(string Message, Exception? Exception)> WarningEntries { get; } = new();
    public List<(string Message, Exception? Exception)> ErrorEntries { get; } = new();

    public void LogInformation(string message)
    {
        InformationMessages.Add(message);
    }

    public void LogWarning(string message, Exception? exception = null)
    {
        WarningEntries.Add((message, exception));
    }

    public void LogError(string message, Exception? exception = null)
    {
        ErrorEntries.Add((message, exception));
    }

    public void LogPerformance(string operation, TimeSpan duration, string? category = null, string? details = null,
        long? processedBytes = null)
    {
    }

    public void LogMemoryUsage(string context, bool includeGcBreakdown = false)
    {
    }

    public IDisposable BeginPerformanceScope(string operation, string? category = null, string? correlationId = null)
    {
        return NoopDisposable.Instance;
    }

    private sealed class NoopDisposable : IDisposable
    {
        public static readonly NoopDisposable Instance = new();

        public void Dispose()
        {
        }
    }
}