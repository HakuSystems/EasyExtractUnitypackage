using System.Formats.Tar;
using System.IO.Compression;
using System.Text;
using EasyExtract.Core.Models;
using EasyExtract.Core.Services;
using EasyExtractCrossPlatform.Tests.Support;
using Xunit;

namespace EasyExtractCrossPlatform.Tests.Services;

/// <summary>
///     A run that wrote nothing used to leave the output directory it had just created sitting
///     there empty, which on disk looks exactly like a package that genuinely held nothing. These
///     tests pin down when the directory is taken back and, more importantly, when it is not.
/// </summary>
public sealed class UnityPackageEmptyOutputTests
{
    [Fact]
    public async Task ExtractAsync_WhenNothingWasRecognized_RemovesTheDirectoryItCreated()
    {
        using var fixture = new PackageFixture();
        await fixture.WriteAsync(("Assets/Scripts/Player.cs", Encoding.UTF8.GetBytes("class Player {}")));

        var result = await ExtractAsync(fixture);

        Assert.Equal(UnityPackageExtractionOutcome.NoAssetsRecognized, result.Outcome);
        Assert.False(Directory.Exists(fixture.OutputDirectory));
    }

    /// <summary>
    ///     The important half. A directory the caller already had belongs to the caller, and an
    ///     empty one they prepared themselves must survive a failed run untouched.
    /// </summary>
    [Fact]
    public async Task ExtractAsync_WhenTheDirectoryExistedBefore_LeavesItAlone()
    {
        using var fixture = new PackageFixture();
        Directory.CreateDirectory(fixture.OutputDirectory);
        await fixture.WriteAsync(("Assets/Scripts/Player.cs", Encoding.UTF8.GetBytes("class Player {}")));

        var result = await ExtractAsync(fixture);

        Assert.Equal(UnityPackageExtractionOutcome.NoAssetsRecognized, result.Outcome);
        Assert.True(Directory.Exists(fixture.OutputDirectory));
    }

    [Fact]
    public async Task ExtractAsync_WithWrittenAssets_KeepsTheDirectory()
    {
        using var fixture = new PackageFixture();
        await fixture.WriteAsync(
            ("asset-one/pathname", Encoding.UTF8.GetBytes("Assets/first.txt")),
            ("asset-one/asset", Encoding.UTF8.GetBytes("first")));

        var result = await ExtractAsync(fixture);

        Assert.Equal(UnityPackageExtractionOutcome.Extracted, result.Outcome);
        Assert.True(Directory.Exists(fixture.OutputDirectory));
        Assert.NotEmpty(Directory.GetFiles(fixture.OutputDirectory, "*", SearchOption.AllDirectories));
    }

    /// <summary>
    ///     A failure that throws leaves the same empty folder as one that does not, so the cleanup
    ///     has to survive the exception path too.
    /// </summary>
    [Fact]
    public async Task ExtractAsync_WhenThePackageIsRejected_StillRemovesTheDirectory()
    {
        using var fixture = new PackageFixture();
        await File.WriteAllTextAsync(fixture.PackagePath, "this is not a gzipped tar archive at all");

        await Assert.ThrowsAsync<InvalidDataException>(() => ExtractAsync(fixture));

        Assert.False(Directory.Exists(fixture.OutputDirectory));
    }

    private static async Task<UnityPackageExtractionResult> ExtractAsync(PackageFixture fixture)
    {
        var service = new UnityPackageExtractionService(new RecordingLogger());
        var options = new UnityPackageExtractionOptions(false, null);
        return await service.ExtractAsync(fixture.PackagePath, fixture.OutputDirectory, options);
    }

    /// <summary>
    ///     Deliberately does not create the output directory, unlike the fixture in
    ///     <see cref="UnityPackageExtractionOutcomeTests" />. Whether it exists beforehand is the
    ///     very thing under test here.
    /// </summary>
    private sealed class PackageFixture : IDisposable
    {
        private readonly string _rootDirectory =
            Path.Combine(Path.GetTempPath(), "EasyExtract.Tests", Guid.NewGuid().ToString("N"));

        public PackageFixture()
        {
            Directory.CreateDirectory(_rootDirectory);
        }

        public string PackagePath => Path.Combine(_rootDirectory, "fixture.unitypackage");
        public string OutputDirectory => Path.Combine(_rootDirectory, "out");

        public void Dispose()
        {
            if (Directory.Exists(_rootDirectory))
                Directory.Delete(_rootDirectory, true);
        }

        public async Task WriteAsync(params (string EntryName, byte[] Content)[] entries)
        {
            await using var fileStream =
                new FileStream(PackagePath, FileMode.Create, FileAccess.Write, FileShare.None);
            await using var gzipStream = new GZipStream(fileStream, CompressionMode.Compress);
            await using var writer = new TarWriter(gzipStream, TarEntryFormat.Pax);

            foreach (var (entryName, content) in entries)
            {
                using var dataStream = new MemoryStream(content, false);
                await writer.WriteEntryAsync(new PaxTarEntry(TarEntryType.RegularFile, entryName)
                {
                    DataStream = dataStream
                });
            }
        }
    }
}
