using EasyExtract.Core;

namespace EasyExtractCrossPlatform.Services;

public sealed class EasyExtractLoggerWrapper : IEasyExtractLogger
{
    public void LogInformation(string message)
    {
        LoggingService.LogInformation(message);
    }

    public void LogWarning(string message, Exception? exception = null)
    {
        LoggingService.LogWarning(message, exception);
    }

    public void LogError(string message, Exception? exception = null)
    {
        LoggingService.LogError(message, exception);
    }

    public void LogPerformance(string operation, TimeSpan duration, string? category = null, string? details = null,
        long? processedBytes = null)
    {
        LoggingService.LogPerformance(operation, duration, category, details, processedBytes);
    }

    public void LogMemoryUsage(string context, bool includeGcBreakdown = false)
    {
        LoggingService.LogMemoryUsage(context, includeGcBreakdown);
    }

    public IDisposable BeginPerformanceScope(string operation, string? category = null, string? correlationId = null)
    {
        return LoggingService.BeginPerformanceScope(operation, category ?? "General", correlationId);
    }
}