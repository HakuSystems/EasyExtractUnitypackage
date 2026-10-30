using EasyExtractCrossPlatform.Services;
using Xunit;

namespace EasyExtractCrossPlatform.Tests.Services;

public sealed class ErrorReportPolicyTests
{
    [Theory]
    [InlineData(21)] // ERROR_NOT_READY
    [InlineData(23)] // ERROR_CRC
    [InlineData(55)] // ERROR_DEV_NOT_EXIST
    [InlineData(433)] // ERROR_NO_SUCH_DEVICE
    [InlineData(1006)] // ERROR_FILE_INVALID
    [InlineData(1117)] // ERROR_IO_DEVICE
    [InlineData(1167)] // ERROR_DEVICE_NOT_CONNECTED
    public void ShouldForward_SkipsFailingOrRemovedStorageDevices(int win32Error)
    {
        Assert.False(ErrorReportPolicy.ShouldForward(new IOException("device gone", ToHResult(win32Error))));
    }

    [Fact]
    public void ShouldForward_SkipsDeviceFailuresWrappedByTheExtractionPipeline()
    {
        var exception = new InvalidOperationException("Extraction failed.",
            new IOException("device gone", ToHResult(1117)));

        Assert.False(ErrorReportPolicy.ShouldForward(exception));
    }

    [Fact]
    public void ShouldForward_KeepsOrdinaryIoFailures()
    {
        Assert.True(ErrorReportPolicy.ShouldForward(new IOException("something else")));
        Assert.True(ErrorReportPolicy.ShouldForward(new IOException("denied", ToHResult(5))));
    }

    [Fact]
    public void ShouldForward_SkipsAMissingDisplay()
    {
        Assert.False(ErrorReportPolicy.ShouldForward(new Exception("XOpenDisplay failed")));
    }

    [Fact]
    public void ShouldForward_KeepsUnrelatedFailures()
    {
        Assert.True(ErrorReportPolicy.ShouldForward(new InvalidOperationException("boom")));
    }

    private static int ToHResult(int win32Error)
    {
        return unchecked((int)(0x80070000u | (uint)win32Error));
    }
}
