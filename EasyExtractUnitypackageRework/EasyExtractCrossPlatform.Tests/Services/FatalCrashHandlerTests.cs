using EasyExtractCrossPlatform.Services;
using Xunit;

namespace EasyExtractCrossPlatform.Tests.Services;

public sealed class FatalCrashHandlerTests
{
    [Fact]
    public void IsOutOfMemory_ReturnsTrueForDirectOutOfMemoryException()
    {
        Assert.True(FatalCrashHandler.IsOutOfMemory(new OutOfMemoryException()));
    }

    [Fact]
    public void IsOutOfMemory_ReturnsTrueForTypeInitializationWrappingOutOfMemory()
    {
        var exception = new TypeInitializationException(
            "Avalonia.Controls.DataValidationErrors", new OutOfMemoryException());

        Assert.True(FatalCrashHandler.IsOutOfMemory(exception));
    }

    [Fact]
    public void IsOutOfMemory_ReturnsTrueForAggregateContainingOutOfMemory()
    {
        var exception = new AggregateException(
            new InvalidOperationException("unrelated"),
            new OutOfMemoryException());

        Assert.True(FatalCrashHandler.IsOutOfMemory(exception));
    }

    [Fact]
    public void IsOutOfMemory_ReturnsFalseForUnrelatedException()
    {
        Assert.False(FatalCrashHandler.IsOutOfMemory(new InvalidOperationException("boom")));
    }

    [Fact]
    public void IsOutOfMemory_ReturnsFalseForNull()
    {
        Assert.False(FatalCrashHandler.IsOutOfMemory(null));
    }

    [Fact]
    public void IsOutOfMemory_DoesNotRecurseForeverOnSelfReferencingAggregate()
    {
        var inner = new InvalidOperationException("leaf");
        Exception exception = inner;
        for (var i = 0; i < 64; i++)
            exception = new AggregateException(exception);

        Assert.False(FatalCrashHandler.IsOutOfMemory(exception));
    }

    [Fact]
    public void BuildUserMessage_MentionsLowMemoryForOutOfMemory()
    {
        var message = FatalCrashHandler.BuildUserMessage(new OutOfMemoryException(), null);

        Assert.Contains("low on memory", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildUserMessage_UsesGenericTextForOtherExceptions()
    {
        var message = FatalCrashHandler.BuildUserMessage(new InvalidOperationException("boom"), null);

        Assert.Contains("unexpected error", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("low on memory", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildUserMessage_ExplainsAMissingDisplayInsteadOfClaimingAReport()
    {
        var message = FatalCrashHandler.BuildUserMessage(new Exception("XOpenDisplay failed"), null);

        Assert.Contains("no display", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("reported", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IsMissingDisplay_IgnoresUnrelatedExceptions()
    {
        Assert.False(FatalCrashHandler.IsMissingDisplay(new InvalidOperationException("boom")));
        Assert.False(FatalCrashHandler.IsMissingDisplay(null));
    }

    [Fact]
    public void BuildUserMessage_IncludesLogPathWhenAvailable()
    {
        var logPath = Path.Combine("C:", "logs", "easyextract_20260714.log");

        var message = FatalCrashHandler.BuildUserMessage(new InvalidOperationException("boom"), logPath);

        Assert.Contains(logPath, message);
    }
}
