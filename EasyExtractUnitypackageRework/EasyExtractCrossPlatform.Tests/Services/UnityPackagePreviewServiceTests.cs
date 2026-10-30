using System.Formats.Tar;
using System.IO.Compression;
using System.Text;
using EasyExtract.Core.Services;
using EasyExtractCrossPlatform.Tests.Support;
using Xunit;

namespace EasyExtractCrossPlatform.Tests.Services;

public sealed class UnityPackagePreviewServiceTests
{
    [Fact]
    public async Task LoadPreviewAsync_ExtractsOversizedModelAssetToTemporaryFile()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "EasyExtract.Tests", Guid.NewGuid().ToString("N"));
        var packagePath = Path.Combine(tempRoot, "fixture.unitypackage");
        Directory.CreateDirectory(tempRoot);

        // Larger than the 8 MB embed limit so the preview must fall back to
        // temporary extraction, which previously only covered audio assets.
        var oversizedModel = new byte[9 * 1024 * 1024];
        Encoding.ASCII.GetBytes("solid nothing").CopyTo(oversizedModel, 0);

        await using (var fileStream = new FileStream(packagePath, FileMode.CreateNew, FileAccess.Write))
        await using (var gzipStream = new GZipStream(fileStream, CompressionMode.Compress))
        await using (var writer = new TarWriter(gzipStream, TarEntryFormat.Pax))
        {
            await WriteEntryAsync(writer, "asset-model/asset", oversizedModel);
            await WriteEntryAsync(writer, "asset-model/pathname", Encoding.UTF8.GetBytes("Assets/big.fbx"));
        }

        var service = new UnityPackagePreviewService(new RecordingLogger());
        string? extractedFilePath = null;

        try
        {
            var preview = await service.LoadPreviewAsync(packagePath);

            var modelAsset = Assert.Single(preview.Assets);
            extractedFilePath = modelAsset.AssetFilePath;

            Assert.False(modelAsset.IsAssetDataTruncated);
            Assert.Null(modelAsset.AssetData);
            Assert.False(string.IsNullOrWhiteSpace(extractedFilePath));
            Assert.True(File.Exists(extractedFilePath));
            Assert.Equal(oversizedModel.LongLength, new FileInfo(extractedFilePath!).Length);
            Assert.False(string.IsNullOrWhiteSpace(preview.TemporaryExtractionRoot));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, true);

            if (extractedFilePath is not null)
            {
                var extractionRoot = Path.GetDirectoryName(extractedFilePath);
                if (!string.IsNullOrWhiteSpace(extractionRoot) && Directory.Exists(extractionRoot))
                    Directory.Delete(extractionRoot, true);
            }
        }
    }

    [Fact]
    public async Task LoadPreviewAsync_StopsEmbeddingAssetsOnceTheBudgetIsSpent()
    {
        // Every asset passes the per-asset limit on its own. Only a total budget keeps an
        // icon pack with thousands of files from filling the heap.
        const int assetBytes = 64 * 1024;
        var tempRoot = CreateTempRoot();
        var packagePath = Path.Combine(tempRoot, "icons.unitypackage");
        await WritePackageAsync(packagePath, 5, assetBytes, ".png");

        var service = new UnityPackagePreviewService(new RecordingLogger(), assetBytes * 2, 1024 * 1024, 1024 * 1024);

        try
        {
            var preview = await service.LoadPreviewAsync(packagePath);

            Assert.Equal(5, preview.Assets.Count);
            Assert.Equal(assetBytes * 5L, preview.TotalAssetSizeBytes);

            var embedded = preview.Assets.Where(asset => asset.AssetData is not null).ToList();
            Assert.Equal(2, embedded.Count);
            Assert.All(embedded, asset => Assert.Equal(CreateAssetBytes(assetBytes), asset.AssetData));
            Assert.All(embedded, asset => Assert.False(asset.IsAssetDataTruncated));

            var dropped = preview.Assets.Where(asset => asset.AssetData is null).ToList();
            Assert.Equal(3, dropped.Count);
            Assert.All(dropped, asset => Assert.True(asset.IsAssetDataTruncated));

            // The thumbnail is what still shows for an asset past the budget.
            Assert.All(preview.Assets, asset => Assert.NotNull(asset.PreviewImageData));
        }
        finally
        {
            Directory.Delete(tempRoot, true);
        }
    }

    [Fact]
    public async Task LoadPreviewAsync_DropsThumbnailsPastTheirOwnBudget()
    {
        var tempRoot = CreateTempRoot();
        var packagePath = Path.Combine(tempRoot, "icons.unitypackage");
        await WritePackageAsync(packagePath, 5, 1024, ".png");

        var service = new UnityPackagePreviewService(new RecordingLogger(), 1024 * 1024, ThumbnailBytes * 2,
            1024 * 1024);

        try
        {
            var preview = await service.LoadPreviewAsync(packagePath);

            Assert.Equal(2, preview.Assets.Count(asset => asset.PreviewImageData is not null));
            Assert.All(preview.Assets, asset => Assert.NotNull(asset.AssetData));
        }
        finally
        {
            Directory.Delete(tempRoot, true);
        }
    }

    [Fact]
    public async Task LoadPreviewAsync_KeepsAudioPastTheBudgetPlayableUpToTheDiskCap()
    {
        const int assetBytes = 64 * 1024;
        var tempRoot = CreateTempRoot();
        var packagePath = Path.Combine(tempRoot, "sounds.unitypackage");
        await WritePackageAsync(packagePath, 5, assetBytes, ".wav");

        var service = new UnityPackagePreviewService(new RecordingLogger(), 0, 1024 * 1024, assetBytes * 2);
        string? extractionRoot = null;

        try
        {
            var preview = await service.LoadPreviewAsync(packagePath);
            extractionRoot = preview.TemporaryExtractionRoot;

            var onDisk = preview.Assets.Where(asset => asset.AssetFilePath is not null).ToList();
            Assert.Equal(2, onDisk.Count);
            Assert.All(onDisk, asset => Assert.True(File.Exists(asset.AssetFilePath)));
            Assert.All(onDisk, asset => Assert.False(asset.IsAssetDataTruncated));
            Assert.Equal(3, preview.Assets.Count(asset => asset.IsAssetDataTruncated));
        }
        finally
        {
            Directory.Delete(tempRoot, true);
            if (extractionRoot is not null && Directory.Exists(extractionRoot))
                Directory.Delete(extractionRoot, true);
        }
    }

    private const int ThumbnailBytes = 2048;

    private static string CreateTempRoot()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "EasyExtract.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        return tempRoot;
    }

    private static byte[] CreateAssetBytes(int length)
    {
        var bytes = new byte[length];
        for (var i = 0; i < bytes.Length; i++)
            bytes[i] = (byte)(i * 31);

        return bytes;
    }

    private static async Task WritePackageAsync(string packagePath, int assetCount, int assetBytes, string extension)
    {
        var asset = CreateAssetBytes(assetBytes);
        var thumbnail = new byte[ThumbnailBytes];

        await using var fileStream = new FileStream(packagePath, FileMode.CreateNew, FileAccess.Write);
        await using var gzipStream = new GZipStream(fileStream, CompressionMode.Compress);
        await using var writer = new TarWriter(gzipStream, TarEntryFormat.Pax);

        for (var i = 0; i < assetCount; i++)
        {
            await WriteEntryAsync(writer, $"asset-{i}/asset", asset);
            await WriteEntryAsync(writer, $"asset-{i}/pathname", Encoding.UTF8.GetBytes($"Assets/item{i}{extension}"));
            await WriteEntryAsync(writer, $"asset-{i}/preview.png", thumbnail);
        }
    }

    private static async Task WriteEntryAsync(TarWriter writer, string entryName, byte[] content)
    {
        using var dataStream = new MemoryStream(content, false);
        var entry = new PaxTarEntry(TarEntryType.RegularFile, entryName)
        {
            DataStream = dataStream
        };

        await writer.WriteEntryAsync(entry);
    }

    [Fact]
    public async Task LoadPreviewAsync_InvalidFormat_LogsWarningInsteadOfError()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "EasyExtract.Tests", Guid.NewGuid().ToString("N"));
        var packagePath = Path.Combine(tempRoot, "invalid.txt");
        Directory.CreateDirectory(tempRoot);
        await File.WriteAllTextAsync(packagePath, "not a unitypackage");

        var logger = new RecordingLogger();
        var service = new UnityPackagePreviewService(logger);

        try
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => service.LoadPreviewAsync(packagePath));

            Assert.NotEmpty(logger.WarningEntries);
            Assert.Empty(logger.ErrorEntries);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, true);
        }
    }
}