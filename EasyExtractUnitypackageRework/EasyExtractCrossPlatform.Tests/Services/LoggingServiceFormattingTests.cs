using EasyExtractCrossPlatform.Services;
using Xunit;

namespace EasyExtractCrossPlatform.Tests.Services;

public sealed class LoggingServiceFormattingTests
{
    [Fact]
    public void FormatLogPayload_ContainsLevelMessageAndExceptionDetails()
    {
        var exception = new InvalidOperationException("something failed");

        var payload = LoggingService.FormatLogPayload("ERROR", "Extraction crashed", exception, null, true);

        Assert.StartsWith("[ERROR]", payload);
        Assert.Contains("Extraction crashed", payload);
        Assert.Contains(nameof(InvalidOperationException), payload);
        Assert.Contains("something failed", payload);
    }

    [Fact]
    public void FormatLogPayload_WithoutStackTraceCaptureUsesTypeAndMessageOnly()
    {
        var exception = new InvalidOperationException("something failed");

        var payload = LoggingService.FormatLogPayload("ERROR", "Extraction crashed", exception, null, false);

        Assert.Contains("System.InvalidOperationException: something failed", payload);
    }

    [Fact]
    public void FormatLogPayload_FallsBackWhenExceptionToStringThrows()
    {
        var exception = new ToStringThrowsException("the real message");

        var payload = LoggingService.FormatLogPayload("ERROR", "Unhandled exception", exception, null, true);

        Assert.Contains("Unhandled exception", payload);
        Assert.Contains("the real message", payload);
        Assert.Contains(nameof(ToStringThrowsException), payload);
    }

    [Fact]
    public void FormatLogPayload_SurvivesExceptionWhereEverythingThrows()
    {
        var exception = new HostileException();

        var payload = LoggingService.FormatLogPayload("ERROR", "Unhandled exception", exception, null, true);

        Assert.Contains("Unhandled exception", payload);
        Assert.Contains(nameof(HostileException), payload);
    }

    [Fact]
    public void FormatLogPayload_AppendsCapturedStackTraceWhenNoException()
    {
        var payload = LoggingService.FormatLogPayload("ERROR", "Manual error", null, "   at Fake.Frame()", true);

        Assert.Contains("Manual error", payload);
        Assert.Contains("at Fake.Frame()", payload);
    }

    private sealed class ToStringThrowsException : Exception
    {
        public ToStringThrowsException(string message) : base(message)
        {
        }

        public override string ToString()
        {
            throw new OutOfMemoryException();
        }
    }

    private sealed class HostileException : Exception
    {
        public override string Message => throw new OutOfMemoryException();

        public override string ToString()
        {
            throw new OutOfMemoryException();
        }
    }
}
