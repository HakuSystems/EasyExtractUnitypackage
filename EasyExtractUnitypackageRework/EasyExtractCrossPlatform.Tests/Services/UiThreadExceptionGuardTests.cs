using System.Runtime.InteropServices;
using EasyExtractCrossPlatform.Services;
using Xunit;

namespace EasyExtractCrossPlatform.Tests.Services;

public sealed class UiThreadExceptionGuardTests
{
    [Fact]
    public void IsRecoverable_UiLayerFailure_IsRecovered()
    {
        var exception = new ArgumentOutOfRangeException("length",
            "Index was out of range. Must be non-negative and less than or equal to the size of the collection.");

        Assert.True(UiThreadExceptionGuard.IsRecoverable(exception));
    }

    [Fact]
    public void IsRecoverable_OutOfMemory_IsNotRecovered()
    {
        Assert.False(UiThreadExceptionGuard.IsRecoverable(new OutOfMemoryException()));
    }

    [Fact]
    public void IsRecoverable_WrappedOutOfMemory_IsNotRecovered()
    {
        var exception = new InvalidOperationException("wrapper", new OutOfMemoryException());

        Assert.False(UiThreadExceptionGuard.IsRecoverable(exception));
    }

    [Fact]
    public void IsRecoverable_ProcessCorruptingFailure_IsNotRecovered()
    {
        Assert.False(UiThreadExceptionGuard.IsRecoverable(new AccessViolationException()));
        Assert.False(UiThreadExceptionGuard.IsRecoverable(new SEHException()));
    }

    [Fact]
    public void IsRecoverable_Null_IsNotRecovered()
    {
        Assert.False(UiThreadExceptionGuard.IsRecoverable(null));
    }
}
