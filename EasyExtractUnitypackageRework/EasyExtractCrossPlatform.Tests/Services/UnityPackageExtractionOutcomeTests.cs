using System.Formats.Tar;
using System.IO.Compression;
using System.Text;
using EasyExtract.Core.Models;
using EasyExtract.Core.Services;
using EasyExtractCrossPlatform.Tests.Support;
using Xunit;

namespace EasyExtractCrossPlatform.Tests.Services;

/// <summary>
///     An extraction that writes zero assets used to be indistinguishable from a successful one,
///     so a package we failed to parse left an empty folder behind and still reported "done".
///     These tests pin down which of the three zero-asset cases the service reports.
/// </summary>
public sealed class UnityPackageExtractionOutcomeTests
{
    [Fact]
    public async Task ExtractAsync_WithUnityLayout_ReportsExtracted()
    {
        using var fixture = new RawPackageFixture();
        await fixture.WriteAsync(
            ("asset-one/pathname", Encoding.UTF8.GetBytes("Assets/first.txt")),
            ("asset-one/asset", Encoding.UTF8.GetBytes("first")));

        var result = await ExtractAsync(fixture);

        Assert.Equal(1, result.AssetsExtracted);
        Assert.Equal(UnityPackageExtractionOutcome.Extracted, result.Outcome);
        Assert.Null(result.OutcomeDetail);
    }

    /// <summary>
    ///     A tar full of plain folders is what a package repacked by some other tool looks like:
    ///     entries exist, none of them are Unity components, nothing can be extracted.
    /// </summary>
    [Fact]
    public async Task ExtractAsync_WithEntriesOutsideUnityLayout_ReportsNoAssetsRecognized()
    {
        using var fixture = new RawPackageFixture();
        await fixture.WriteAsync(
            ("Assets/Scripts/Player.cs", Encoding.UTF8.GetBytes("public class Player {}")),
            ("Assets/Readme.md", Encoding.UTF8.GetBytes("# hello")),
            ("ProjectSettings/Tags.asset", Encoding.UTF8.GetBytes("tags:")));

        var result = await ExtractAsync(fixture);

        Assert.Equal(0, result.AssetsExtracted);
        Assert.Equal(UnityPackageExtractionOutcome.NoAssetsRecognized, result.Outcome);
        Assert.Contains("none of them use Unity's asset layout", result.OutcomeDetail);
    }

    /// <summary>
    ///     Marks the lower edge of what the outcome guard is responsible for: a package with no
    ///     entries at all never reaches it, the format check rejects the file first. If this ever
    ///     starts returning a result instead of throwing, the guard needs a case for it.
    /// </summary>
    [Fact]
    public async Task ExtractAsync_WithEmptyPackage_IsRejectedBeforeTheOutcomeGuard()
    {
        using var fixture = new RawPackageFixture();
        await fixture.WriteAsync();

        await Assert.ThrowsAsync<InvalidDataException>(() => ExtractAsync(fixture));
    }

    /// <summary>
    ///     An asset with a path but no content is parsed fine, it simply has nothing to write.
    ///     That still counts as unrecognised because no file reached the disk and nothing was
    ///     there before either.
    /// </summary>
    [Fact]
    public async Task ExtractAsync_WithAssetsMissingContent_ReportsNoAssetsRecognized()
    {
        using var fixture = new RawPackageFixture();
        await fixture.WriteAsync(
            ("asset-one/pathname", Encoding.UTF8.GetBytes("Assets/first.txt")));

        var result = await ExtractAsync(fixture);

        Assert.Equal(UnityPackageExtractionOutcome.NoAssetsRecognized, result.Outcome);
        Assert.Contains("had both a path and", result.OutcomeDetail);
    }

    /// <summary>
    ///     Extracting the same package twice must not raise the alarm the second time. This is the
    ///     case a naive "zero assets means failure" check would get wrong.
    /// </summary>
    [Fact]
    public async Task ExtractAsync_WhenEverythingIsAlreadyOnDisk_ReportsNothingToDo()
    {
        using var fixture = new RawPackageFixture();
        await fixture.WriteAsync(
            ("asset-one/pathname", Encoding.UTF8.GetBytes("Assets/first.txt")),
            ("asset-one/asset", Encoding.UTF8.GetBytes("first")));

        var first = await ExtractAsync(fixture);
        Assert.Equal(UnityPackageExtractionOutcome.Extracted, first.Outcome);

        var second = await ExtractAsync(fixture);

        Assert.Equal(0, second.AssetsExtracted);
        Assert.Equal(UnityPackageExtractionOutcome.NothingToDo, second.Outcome);
        Assert.Contains("already extracted and unchanged", second.OutcomeDetail);
    }

    /// <summary>
    ///     A filter that excludes everything is a deliberate no-op by the caller, not a package
    ///     the service failed to read.
    /// </summary>
    [Fact]
    public async Task ExtractAsync_WithFilterThatMatchesNothing_ReportsNothingToDo()
    {
        using var fixture = new RawPackageFixture();
        await fixture.WriteAsync(
            ("asset-one/pathname", Encoding.UTF8.GetBytes("Assets/first.txt")),
            ("asset-one/asset", Encoding.UTF8.GetBytes("first")));

        var result = await ExtractAsync(fixture, new[] { "asset-does-not-exist" });

        Assert.Equal(0, result.AssetsExtracted);
        Assert.Equal(UnityPackageExtractionOutcome.NothingToDo, result.Outcome);
        Assert.Equal("None of the selected assets are in this package.", result.OutcomeDetail);
    }

    private static async Task<UnityPackageExtractionResult> ExtractAsync(
        RawPackageFixture fixture,
        IReadOnlyCollection<string>? includeAssetKeys = null)
    {
        var service = new UnityPackageExtractionService(new RecordingLogger());
        var options = new UnityPackageExtractionOptions(false, null, IncludeAssetKeys: includeAssetKeys);
        return await service.ExtractAsync(fixture.PackagePath, fixture.OutputDirectory, options);
    }

    /// <summary>
    ///     Writes tar entries verbatim, so a fixture can describe layouts that are not valid
    ///     Unity packages. UnityPackageBuilder always produces the correct layout and cannot.
    /// </summary>
    private sealed class RawPackageFixture : IDisposable
    {
        private readonly string _rootDirectory =
            Path.Combine(Path.GetTempPath(), "EasyExtract.Tests", Guid.NewGuid().ToString("N"));

        public RawPackageFixture()
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
