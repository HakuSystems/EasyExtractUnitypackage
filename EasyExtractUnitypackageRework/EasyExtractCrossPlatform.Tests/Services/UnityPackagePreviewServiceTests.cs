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