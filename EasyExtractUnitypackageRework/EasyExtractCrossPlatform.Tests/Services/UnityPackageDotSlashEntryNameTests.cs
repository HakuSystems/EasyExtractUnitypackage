using System.Formats.Tar;
using System.IO.Compression;
using System.Text;
using EasyExtract.Core.Models;
using EasyExtract.Core.Services;
using EasyExtractCrossPlatform.Tests.Support;
using Xunit;

namespace EasyExtractCrossPlatform.Tests.Services;

/// <summary>
///     Packages that were repacked outside Unity (macOS bsdtar writes "./guid/asset" plus
///     AppleDouble "._" siblings) must extract, preview and scan just like Unity's own layout.
/// </summary>
public sealed class UnityPackageDotSlashEntryNameTests
{
    private const string WebhookUrl =
        "https://discord.com/api/webhooks/123456789012345678/" +
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789abcdefgh";

    [Fact]
    public async Task ExtractAsync_WithDotSlashPrefixedEntries_ExtractsAssets()
    {
        using var fixture = new PackageFixture();
        await fixture.WriteAsync(
            ("./", null),
            ("./asset-one/", null),
            ("./asset-one/._asset", Encoding.UTF8.GetBytes("apple double junk")),
            ("./asset-one/asset", Encoding.UTF8.GetBytes("first")),
            ("./asset-one/asset.meta", Encoding.UTF8.GetBytes("guid: 1")),
            ("./asset-one/pathname", Encoding.UTF8.GetBytes("Assets/first.txt")),
            ("./asset-two/asset", Encoding.UTF8.GetBytes("second")),
            ("./asset-two/pathname", Encoding.UTF8.GetBytes("Assets/second.txt")));

        var service = new UnityPackageExtractionService(new RecordingLogger());

        var result = await service.ExtractAsync(
            fixture.PackagePath,
            fixture.OutputDirectory,
            new UnityPackageExtractionOptions(false, null));

        Assert.Equal(2, result.AssetsExtracted);
        Assert.Equal("first", await File.ReadAllTextAsync(
            Path.Combine(fixture.OutputDirectory, "Assets", "first.txt")));
        Assert.True(File.Exists(Path.Combine(fixture.OutputDirectory, "Assets", "first.txt.meta")));
        Assert.True(File.Exists(Path.Combine(fixture.OutputDirectory, "Assets", "second.txt")));
    }

    [Fact]
    public async Task LoadPreviewAsync_WithDotSlashPrefixedEntries_ListsAssets()
    {
        using var fixture = new PackageFixture();
        await fixture.WriteAsync(
            ("./asset-one/asset", Encoding.UTF8.GetBytes("first")),
            ("./asset-one/pathname", Encoding.UTF8.GetBytes("Assets/first.txt")));

        var service = new UnityPackagePreviewService(new RecordingLogger());

        var preview = await service.LoadPreviewAsync(fixture.PackagePath);

        var asset = Assert.Single(preview.Assets);
        Assert.Equal(Path.Combine("Assets", "first.txt"), asset.RelativePath);
        Assert.Equal("asset-one", asset.AssetKey);
    }

    [Fact]
    public async Task ScanPackageAsync_WithDotSlashPrefixedEntries_StillInspectsAssetContent()
    {
        using var fixture = new PackageFixture();
        await fixture.WriteAsync(
            ("./asset-one/asset", Encoding.UTF8.GetBytes($"var url = \"{WebhookUrl}\";")),
            ("./asset-one/pathname", Encoding.UTF8.GetBytes("Assets/Stealer.cs")));

        var service = new MaliciousCodeDetectionService(new RecordingLogger());

        var result = await service.ScanPackageAsync(fixture.PackagePath);

        Assert.True(result.IsMalicious);
        Assert.Contains(result.Threats, threat => threat.Type == MaliciousThreatType.DiscordWebhook);
    }

    private sealed class PackageFixture : IDisposable
    {
        private readonly string _rootDirectory =
            Path.Combine(Path.GetTempPath(), "EasyExtract.Tests", Guid.NewGuid().ToString("N"));

        public PackageFixture()
        {
            Directory.CreateDirectory(_rootDirectory);
            Directory.CreateDirectory(OutputDirectory);
        }

        public string PackagePath => Path.Combine(_rootDirectory, "fixture.unitypackage");
        public string OutputDirectory => Path.Combine(_rootDirectory, "out");

        public void Dispose()
        {
            if (Directory.Exists(_rootDirectory))
                Directory.Delete(_rootDirectory, true);
        }

        public async Task WriteAsync(params (string EntryName, byte[]? Content)[] entries)
        {
            await using var fileStream =
                new FileStream(PackagePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await using var gzipStream = new GZipStream(fileStream, CompressionMode.Compress);
            await using var writer = new TarWriter(gzipStream, TarEntryFormat.Pax);

            foreach (var (entryName, content) in entries)
            {
                if (content is null)
                {
                    await writer.WriteEntryAsync(new PaxTarEntry(TarEntryType.Directory, entryName));
                    continue;
                }

                using var dataStream = new MemoryStream(content, false);
                var entry = new PaxTarEntry(TarEntryType.RegularFile, entryName)
                {
                    DataStream = dataStream
                };

                await writer.WriteEntryAsync(entry);
            }
        }
    }
}
