using System.Formats.Tar;
using System.IO.Compression;
using System.Text;
using EasyExtract.Core;
using EasyExtract.Core.Models;
using EasyExtract.Core.Services;
using Xunit;

namespace EasyExtractCrossPlatform.Tests.Services;

public sealed class UnityPackageExtractionServiceSecurityTests
{
    [Theory]
    [InlineData("../evil.txt", "outside")]
    [InlineData("/etc/passwd", "rooted")]
    [InlineData("C:/Windows/win.ini", "drive-qualified")]
    [InlineData("\\\\server\\share\\loot.txt", "UNC")]
    public async Task ExtractAsync_RejectsUnsafeArchivePaths(string archivePath, string _)
    {
        await using var harness = await ExtractionHarness.CreateAsync();
        await harness.CreatePackageAsync(
            ("asset-one/pathname", archivePath),
            ("asset-one/asset", new byte[] { 0x1, 0x2, 0x3 }));

        var service = harness.CreateService();

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => service.ExtractAsync(
            harness.PackagePath,
            harness.OutputDirectory,
            new UnityPackageExtractionOptions(false, null)));

        Assert.Contains("path", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(Directory.EnumerateFileSystemEntries(harness.OutputDirectory));
    }

    [Fact]
    public async Task ExtractAsync_RejectsNormalizedPathCollisions()
    {
        await using var harness = await ExtractionHarness.CreateAsync();
        await harness.CreatePackageAsync(
            ("asset-one/pathname", "Assets/Foo?.txt"),
            ("asset-one/asset", Encoding.UTF8.GetBytes("one")),
            ("asset-two/pathname", "Assets/Foo*.txt"),
            ("asset-two/asset", Encoding.UTF8.GetBytes("two")));

        var service = harness.CreateService();

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => service.ExtractAsync(
            harness.PackagePath,
            harness.OutputDirectory,
            new UnityPackageExtractionOptions(false, null)));

        Assert.Contains("collision", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(Directory.EnumerateFileSystemEntries(harness.OutputDirectory));
    }

    [Fact]
    public async Task ExtractAsync_AbortsWhenLimitIsExceededAfterEarlierValidEntries()
    {
        await using var harness = await ExtractionHarness.CreateAsync();
        await harness.CreatePackageAsync(
            ("asset-one/pathname", "Assets/first.txt"),
            ("asset-one/asset", Encoding.UTF8.GetBytes("first")),
            ("asset-two/pathname", "Assets/second.txt"),
            ("asset-two/asset", Enumerable.Repeat((byte)'B', 128).ToArray()));

        var service = harness.CreateService();
        var options = new UnityPackageExtractionOptions(
            false,
            null,
            new UnityPackageExtractionLimits
            {
                MaxAssetBytes = 64,
                MaxPackageBytes = 1024,
                MaxAssets = 10
            });

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => service.ExtractAsync(
            harness.PackagePath,
            harness.OutputDirectory,
            options));

        Assert.Contains("limit", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(Path.Combine(harness.OutputDirectory, "Assets", "first.txt")));
    }

    [Fact]
    public async Task ExtractAsync_AbortsWhenTrackedAssetStatesExceedDerivedLimit()
    {
        await using var harness = await ExtractionHarness.CreateAsync();
        await harness.CreatePackageAsync(
            ("asset-one/pathname", "Assets/one.txt"),
            ("asset-two/pathname", "Assets/two.txt"),
            ("asset-three/pathname", "Assets/three.txt"),
            ("asset-four/pathname", "Assets/four.txt"),
            ("asset-five/pathname", "Assets/five.txt"));

        var service = harness.CreateService();
        var options = new UnityPackageExtractionOptions(
            false,
            null,
            new UnityPackageExtractionLimits
            {
                MaxAssetBytes = 1024,
                MaxPackageBytes = 4096,
                MaxAssets = 1
            });

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => service.ExtractAsync(
            harness.PackagePath,
            harness.OutputDirectory,
            options));

        Assert.Contains("tracked asset states", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(Directory.EnumerateFileSystemEntries(harness.OutputDirectory));
    }

    [Fact]
    public async Task ExtractAsync_AbortsWhenTarEntryCountExceedsDerivedLimit()
    {
        await using var harness = await ExtractionHarness.CreateAsync();
        await harness.CreatePackageAsync(
            ("asset-one/pathname", "Assets/one.txt"),
            ("asset-one/ignored-1", Encoding.UTF8.GetBytes("x")),
            ("asset-one/ignored-2", Encoding.UTF8.GetBytes("x")),
            ("asset-one/ignored-3", Encoding.UTF8.GetBytes("x")),
            ("asset-one/ignored-4", Encoding.UTF8.GetBytes("x")),
            ("asset-one/ignored-5", Encoding.UTF8.GetBytes("x")),
            ("asset-one/ignored-6", Encoding.UTF8.GetBytes("x")),
            ("asset-one/ignored-7", Encoding.UTF8.GetBytes("x")),
            ("asset-one/ignored-8", Encoding.UTF8.GetBytes("x")),
            ("asset-one/ignored-9", Encoding.UTF8.GetBytes("x")),
            ("asset-one/ignored-10", Encoding.UTF8.GetBytes("x")),
            ("asset-one/ignored-11", Encoding.UTF8.GetBytes("x")));

        var service = harness.CreateService();
        var options = new UnityPackageExtractionOptions(
            false,
            null,
            new UnityPackageExtractionLimits
            {
                MaxAssetBytes = 1024,
                MaxPackageBytes = 4096,
                MaxAssets = 1
            });

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => service.ExtractAsync(
            harness.PackagePath,
            harness.OutputDirectory,
            options));

        Assert.Contains("TAR entries", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(Directory.EnumerateFileSystemEntries(harness.OutputDirectory));
    }

    private sealed class ExtractionHarness : IAsyncDisposable
    {
        private readonly string _rootDirectory;

        private ExtractionHarness(string rootDirectory, string packagePath, string outputDirectory)
        {
            _rootDirectory = rootDirectory;
            PackagePath = packagePath;
            OutputDirectory = outputDirectory;
        }

        public string OutputDirectory { get; }
        public string PackagePath { get; }

        public ValueTask DisposeAsync()
        {
            if (Directory.Exists(_rootDirectory))
                Directory.Delete(_rootDirectory, true);

            return ValueTask.CompletedTask;
        }

        public static Task<ExtractionHarness> CreateAsync()
        {
            var rootDirectory = Path.Combine(Path.GetTempPath(), "EasyExtract.Tests", Guid.NewGuid().ToString("N"));
            var packagePath = Path.Combine(rootDirectory, "fixture.unitypackage");
            var outputDirectory = Path.Combine(rootDirectory, "out");

            Directory.CreateDirectory(rootDirectory);
            Directory.CreateDirectory(outputDirectory);

            return Task.FromResult(new ExtractionHarness(rootDirectory, packagePath, outputDirectory));
        }

        public UnityPackageExtractionService CreateService()
        {
            return new UnityPackageExtractionService(new TestLogger());
        }

        public async Task CreatePackageAsync(params (string EntryName, object Content)[] entries)
        {
            await using var fileStream =
                new FileStream(PackagePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await using var gzipStream = new GZipStream(fileStream, CompressionMode.Compress);
            await using var writer = new TarWriter(gzipStream, TarEntryFormat.Pax, false);

            foreach (var (entryName, content) in entries)
            {
                var data = content switch
                {
                    string text => Encoding.UTF8.GetBytes(text),
                    byte[] bytes => bytes,
                    _ => throw new InvalidOperationException($"Unsupported tar payload for '{entryName}'.")
                };

                using var dataStream = new MemoryStream(data, false);
                var entry = new PaxTarEntry(TarEntryType.RegularFile, entryName)
                {
                    DataStream = dataStream
                };

                await writer.WriteEntryAsync(entry);
            }
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