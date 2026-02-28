using System.IO.Compression;
using EasyExtract.Core;
using EasyExtract.Core.Models;
using EasyExtract.Core.Services;
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

            var service = new UnityPackageExtractionService(new TestLogger());
            var options = new UnityPackageExtractionOptions(false, null);

            var exception = await Assert.ThrowsAsync<InvalidDataException>(async () =>
                await service.ExtractAsync(packagePath, outputDirectory, options));

            Assert.DoesNotContain("gzip checksum mismatch", exception.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("invalid gzip data", exception.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.Contains("valid .unitypackage", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, true);
        }
    }

    private sealed class TestLogger : IEasyExtractLogger
    {
        public void LogInformation(string message)
        {
        }

        public void LogWarning(string message, Exception? exception = null)
        {
        }

        public void LogError(string message, Exception? exception = null)
        {
        }

        public void LogPerformance(string operation, TimeSpan duration, string? category = null, string? details = null,
            long? processedBytes = null)
        {
        }

        public void LogMemoryUsage(string context, bool includeGcBreakdown = false)
        {
        }

        public IDisposable BeginPerformanceScope(string operation, string? category = null,
            string? correlationId = null)
        {
            return NoOpDisposable.Instance;
        }
    }

    private sealed class NoOpDisposable : IDisposable
    {
        public static readonly NoOpDisposable Instance = new();

        public void Dispose()
        {
        }
    }
}