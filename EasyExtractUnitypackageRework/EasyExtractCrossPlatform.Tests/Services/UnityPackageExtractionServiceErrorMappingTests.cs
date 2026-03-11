using System.IO.Compression;
using EasyExtract.Core.Models;
using EasyExtract.Core.Services;
using EasyExtractCrossPlatform.Tests.Support;
using Xunit;

namespace EasyExtractCrossPlatform.Tests.Services;

public sealed class UnityPackageExtractionServiceErrorMappingTests
{
    [Fact]
    public async Task ExtractAsync_GzipContainerWithInvalidTar_DoesNotMislabelAsChecksumMismatch()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "EasyExtract.Tests", Guid.NewGuid().ToString("N"));
        var packagePath = Path.Combine(tempRoot, "invalid.unitypackage");
        var outputDirectory = Path.Combine(tempRoot, "out");
        Directory.CreateDirectory(tempRoot);

        try
        {
            await using (var packageStream = new FileStream(packagePath, FileMode.CreateNew, FileAccess.Write,
                             FileShare.None))
            await using (var gzipStream = new GZipStream(packageStream, CompressionMode.Compress))
            {
                var invalidTarPayload = Enumerable.Repeat((byte)'X', 1024).ToArray();
                await gzipStream.WriteAsync(invalidTarPayload);
            }

            var logger = new RecordingLogger();
            var service = new UnityPackageExtractionService(logger);
            var options = new UnityPackageExtractionOptions(false, null);

            var exception = await Assert.ThrowsAsync<InvalidDataException>(async () =>
                await service.ExtractAsync(packagePath, outputDirectory, options));

            Assert.DoesNotContain("gzip checksum mismatch", exception.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("invalid gzip data", exception.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.Contains("valid .unitypackage", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.NotEmpty(logger.WarningEntries);
            Assert.Empty(logger.ErrorEntries);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, true);
        }
    }

    [Fact]
    public void DefaultMaxPackageBytes_Is16GiB()
    {
        Assert.Equal(16L * 1024 * 1024 * 1024, UnityPackageExtractionLimits.DefaultMaxPackageBytes);
    }
}