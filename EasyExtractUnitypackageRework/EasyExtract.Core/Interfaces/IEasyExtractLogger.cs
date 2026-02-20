namespace EasyExtract.Core;

public interface IEasyExtractLogger
{
    void LogInformation(string message);
    void LogWarning(string message, Exception? exception = null);
    void LogError(string message, Exception? exception = null);

    void LogPerformance(string operation, TimeSpan duration, string? category = null, string? details = null,
        long? processedBytes = null);

    void LogMemoryUsage(string context, bool includeGcBreakdown = false);

    // Abstracting Scope
    IDisposable BeginPerformanceScope(string operation, string? category = null, string? correlationId = null);
}