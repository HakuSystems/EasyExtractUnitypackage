using System.Formats.Tar;
using System.IO.Compression;
using System.Text;
using EasyExtract.Core.Models;
using EasyExtract.Core.Services;
using EasyExtractCrossPlatform.Tests.Support;
using Xunit;

namespace EasyExtractCrossPlatform.Tests.Services;

public sealed class UnityPackageExtractionServiceFilterTests
{
    [Fact]
    public async Task ExtractAsync_WithIncludeAssetKeys_ExtractsOnlySelectedAssets()
    {
        await using var harness = await FilterHarness.CreateAsync();
        // Real packages order the components alphabetically: the pathname entry
        // arrives after asset and asset.meta.
        await harness.CreatePackageAsync(
            ("asset-one/asset", Encoding.UTF8.GetBytes("keep me")),
            ("asset-one/asset.meta", Encoding.UTF8.GetBytes("guid: 1")),
            ("asset-one/pathname", "Assets/keep.txt"),
            ("asset-two/asset", Encoding.UTF8.GetBytes("drop me")),
            ("asset-two/pathname", "Assets/drop.txt"));

        var service = harness.CreateService();
        var options = new UnityPackageExtractionOptions(false, null, IncludeAssetKeys: new[]
        {
            "asset-one"
        });

        var result = await service.ExtractAsync(harness.PackagePath, harness.OutputDirectory, options);

        Assert.Equal(1, result.AssetsExtracted);
        Assert.True(File.Exists(Path.Combine(harness.OutputDirectory, "Assets", "keep.txt")));
        Assert.True(File.Exists(Path.Combine(harness.OutputDirectory, "Assets", "keep.txt.meta")));
        Assert.False(File.Exists(Path.Combine(harness.OutputDirectory, "Assets", "drop.txt")));
    }

    [Fact]
    public async Task ExtractAsync_WithoutIncludeAssetKeys_ExtractsEverything()
    {
        await using var harness = await FilterHarness.CreateAsync();
        await harness.CreatePackageAsync(
            ("asset-one/pathname", "Assets/first.txt"),
            ("asset-one/asset", Encoding.UTF8.GetBytes("first")),
            ("asset-two/pathname", "Assets/second.txt"),
            ("asset-two/asset", Encoding.UTF8.GetBytes("second")));

        var service = harness.CreateService();
        var options = new UnityPackageExtractionOptions(false, null);

        var result = await service.ExtractAsync(harness.PackagePath, harness.OutputDirectory, options);

        Assert.Equal(2, result.AssetsExtracted);
        Assert.True(File.Exists(Path.Combine(harness.OutputDirectory, "Assets", "first.txt")));
        Assert.True(File.Exists(Path.Combine(harness.OutputDirectory, "Assets", "second.txt")));
    }

    [Fact]
    public async Task ExtractAsync_WithEmptyIncludeAssetKeys_ExtractsNothing()
    {
        await using var harness = await FilterHarness.CreateAsync();
        await harness.CreatePackageAsync(
            ("asset-one/pathname", "Assets/first.txt"),
            ("asset-one/asset", Encoding.UTF8.GetBytes("first")));

        var service = harness.CreateService();
        var options = new UnityPackageExtractionOptions(false, null, IncludeAssetKeys: Array.Empty<string>());

        var result = await service.ExtractAsync(harness.PackagePath, harness.OutputDirectory, options);

        Assert.Equal(0, result.AssetsExtracted);
        Assert.False(File.Exists(Path.Combine(harness.OutputDirectory, "Assets", "first.txt")));
    }

    [Fact]
    public async Task ExtractAsync_IncludeAssetKeys_MatchCaseInsensitively()
    {
        await using var harness = await FilterHarness.CreateAsync();
        await harness.CreatePackageAsync(
            ("Asset-One/pathname", "Assets/keep.txt"),
            ("Asset-One/asset", Encoding.UTF8.GetBytes("keep me")));

        var service = harness.CreateService();
        var options = new UnityPackageExtractionOptions(false, null, IncludeAssetKeys: new[]
        {
            "asset-one"
        });

        var result = await service.ExtractAsync(harness.PackagePath, harness.OutputDirectory, options);

        Assert.Equal(1, result.AssetsExtracted);
        Assert.True(File.Exists(Path.Combine(harness.OutputDirectory, "Assets", "keep.txt")));
    }

    [Fact]
    public async Task ExtractAsync_IncludeAssetKeys_IgnoreBlankEntries()
    {
        await using var harness = await FilterHarness.CreateAsync();
        await harness.CreatePackageAsync(
            ("asset-one/pathname", "Assets/keep.txt"),
            ("asset-one/asset", Encoding.UTF8.GetBytes("keep me")),
            ("asset-two/pathname", "Assets/drop.txt"),
            ("asset-two/asset", Encoding.UTF8.GetBytes("drop me")));

        var service = harness.CreateService();
        var options = new UnityPackageExtractionOptions(false, null, IncludeAssetKeys: new[]
        {
            "  ", "asset-one", ""
        });

        var result = await service.ExtractAsync(harness.PackagePath, harness.OutputDirectory, options);

        Assert.Equal(1, result.AssetsExtracted);
        Assert.True(File.Exists(Path.Combine(harness.OutputDirectory, "Assets", "keep.txt")));
        Assert.False(File.Exists(Path.Combine(harness.OutputDirectory, "Assets", "drop.txt")));
    }

    private sealed class FilterHarness : IAsyncDisposable
    {
        private readonly string _rootDirectory;

        private FilterHarness(string rootDirectory, string packagePath, string outputDirectory)
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

        public static Task<FilterHarness> CreateAsync()
        {
            var rootDirectory = Path.Combine(Path.GetTempPath(), "EasyExtract.Tests", Guid.NewGuid().ToString("N"));
            var packagePath = Path.Combine(rootDirectory, "fixture.unitypackage");
            var outputDirectory = Path.Combine(rootDirectory, "out");

            Directory.CreateDirectory(rootDirectory);
            Directory.CreateDirectory(outputDirectory);

            return Task.FromResult(new FilterHarness(rootDirectory, packagePath, outputDirectory));
        }

        public UnityPackageExtractionService CreateService(RecordingLogger? logger = null)
        {
            return new UnityPackageExtractionService(logger ?? new RecordingLogger());
        }

        public async Task CreatePackageAsync(params (string EntryName, object Content)[] entries)
        {
            await using var fileStream =
                new FileStream(PackagePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await using var gzipStream = new GZipStream(fileStream, CompressionMode.Compress);
            await using var writer = new TarWriter(gzipStream, TarEntryFormat.Pax);

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
}
