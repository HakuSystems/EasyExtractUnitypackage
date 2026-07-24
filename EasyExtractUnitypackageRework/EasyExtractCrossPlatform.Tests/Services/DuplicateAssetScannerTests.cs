using System.Text;
using EasyExtract.Core.Models;
using EasyExtract.Core.Services;
using EasyExtractCrossPlatform.Tests.Support;
using Xunit;

namespace EasyExtractCrossPlatform.Tests.Services;

public sealed class DuplicateAssetScannerTests : IDisposable
{
    private readonly string _root;

    public DuplicateAssetScannerTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "EasyExtractTests", "Duplicates", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, true);
    }

    [Fact]
    public async Task FingerprintPackageAsync_HashesAssetsByContent()
    {
        var package = await new UnityPackageBuilder()
            .WithAsset("Assets/Shader.shader", "shader body")
            .WithAsset("Assets/Texture.png", "texture body")
            .WriteAsync(_root, "one.unitypackage");

        var result = await CreateScanner().FingerprintPackageAsync(package);

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.Assets.Count);
        Assert.Contains(result.Assets, asset => asset.AssetPath == "Assets/Shader.shader");
        Assert.All(result.Assets, asset => Assert.Equal(64, asset.ContentHash.Length));
        Assert.All(result.Assets, asset => Assert.True(asset.SizeBytes > 0));
    }

    [Fact]
    public async Task FingerprintPackageAsync_GivesIdenticalContentTheSameHashUnderDifferentNames()
    {
        var body = Encoding.UTF8.GetBytes("the very same bytes");

        var first = await new UnityPackageBuilder()
            .WithAsset("Assets/Poiyomi/Shader.shader", body)
            .WriteAsync(_root, "first.unitypackage");
        var second = await new UnityPackageBuilder()
            .WithAsset("Assets/Vendor/Copies/Renamed.shader", body)
            .WriteAsync(_root, "second.unitypackage");

        var scanner = CreateScanner();
        var firstResult = await scanner.FingerprintPackageAsync(first);
        var secondResult = await scanner.FingerprintPackageAsync(second);

        Assert.Equal(firstResult.Assets[0].ContentHash, secondResult.Assets[0].ContentHash);
    }

    [Fact]
    public async Task FingerprintPackageAsync_ReportsUnreadablePackagesInsteadOfThrowing()
    {
        var broken = Path.Combine(_root, "broken.unitypackage");
        await File.WriteAllTextAsync(broken, "this is definitely not a package");

        var result = await CreateScanner().FingerprintPackageAsync(broken);

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Error);
        Assert.Empty(result.Assets);
    }

    [Fact]
    public async Task FingerprintPackageAsync_ReportsMissingPackages()
    {
        var result = await CreateScanner()
            .FingerprintPackageAsync(Path.Combine(_root, "does-not-exist.unitypackage"));

        Assert.False(result.Succeeded);
        Assert.Empty(result.Assets);
    }

    [Fact]
    public async Task FingerprintPackageAsync_SkipsAssetsWithoutAPathname()
    {
        var package = await new UnityPackageBuilder()
            .WithAsset("Assets/Kept.png", "kept")
            .WriteAsync(_root, "partial.unitypackage");

        var result = await CreateScanner().FingerprintPackageAsync(package);

        Assert.Single(result.Assets);
        Assert.Equal("Assets/Kept.png", result.Assets[0].AssetPath);
    }

    [Fact]
    public void BuildReport_GroupsIdenticalContentAcrossPackages()
    {
        var report = DuplicateAssetScanner.BuildReport(
        [
            Fingerprints("/lib/a.unitypackage", ("Assets/Shared.png", "HASH-A", 1000)),
            Fingerprints("/lib/b.unitypackage", ("Assets/Other/Shared.png", "HASH-A", 1000)),
            Fingerprints("/lib/c.unitypackage", ("Assets/Unique.png", "HASH-B", 500))
        ]);

        var group = Assert.Single(report.Groups);
        Assert.Equal("HASH-A", group.ContentHash);
        Assert.Equal(2, group.Occurrences.Count);
        Assert.Equal(2, group.DistinctPackageCount);
        Assert.True(group.SpansMultiplePackages);
        Assert.Equal(1000, group.WastedBytes);
        Assert.Equal(1000, report.TotalWastedBytes);
        Assert.Equal(3, report.PackagesScanned);
        Assert.Equal(0, report.PackagesFailed);
    }

    [Fact]
    public void BuildReport_CountsEveryExtraCopyAsWaste()
    {
        var report = DuplicateAssetScanner.BuildReport(
        [
            Fingerprints("/lib/a.unitypackage", ("Assets/Shared.png", "HASH-A", 250)),
            Fingerprints("/lib/b.unitypackage", ("Assets/Shared.png", "HASH-A", 250)),
            Fingerprints("/lib/c.unitypackage", ("Assets/Shared.png", "HASH-A", 250)),
            Fingerprints("/lib/d.unitypackage", ("Assets/Shared.png", "HASH-A", 250))
        ]);

        var group = Assert.Single(report.Groups);
        Assert.Equal(750, group.WastedBytes);
        Assert.Equal(3, report.TotalDuplicateCopies);
    }

    [Fact]
    public void BuildReport_SortsByWastedSpace()
    {
        var report = DuplicateAssetScanner.BuildReport(
        [
            Fingerprints("/lib/a.unitypackage",
                ("Assets/Small.png", "HASH-SMALL", 10), ("Assets/Big.png", "HASH-BIG", 9000)),
            Fingerprints("/lib/b.unitypackage",
                ("Assets/Small.png", "HASH-SMALL", 10), ("Assets/Big.png", "HASH-BIG", 9000))
        ]);

        Assert.Equal(2, report.Groups.Count);
        Assert.Equal("HASH-BIG", report.Groups[0].ContentHash);
        Assert.Equal("HASH-SMALL", report.Groups[1].ContentHash);
    }

    [Fact]
    public void BuildReport_FlagsDuplicatesInsideASinglePackage()
    {
        var report = DuplicateAssetScanner.BuildReport(
        [
            Fingerprints("/lib/a.unitypackage",
                ("Assets/One.png", "HASH-A", 100), ("Assets/Copy/One.png", "HASH-A", 100))
        ]);

        var group = Assert.Single(report.Groups);
        Assert.Equal(1, group.DistinctPackageCount);
        Assert.False(group.SpansMultiplePackages);
        Assert.Equal(100, group.WastedBytes);
    }

    [Fact]
    public void BuildReport_KeepsFailedPackagesOutOfTheGroupsButCountsThem()
    {
        var report = DuplicateAssetScanner.BuildReport(
        [
            Fingerprints("/lib/a.unitypackage", ("Assets/Shared.png", "HASH-A", 100)),
            Fingerprints("/lib/b.unitypackage", ("Assets/Shared.png", "HASH-A", 100)),
            new PackageFingerprintResult("/lib/broken.unitypackage", [], "unreadable")
        ]);

        Assert.Single(report.Groups);
        Assert.Equal(2, report.PackagesScanned);
        Assert.Equal(1, report.PackagesFailed);
        Assert.Equal("/lib/broken.unitypackage", Assert.Single(report.FailedPackages));
    }

    [Fact]
    public void BuildReport_WithoutDuplicatesReportsNothing()
    {
        var report = DuplicateAssetScanner.BuildReport(
        [
            Fingerprints("/lib/a.unitypackage", ("Assets/A.png", "HASH-A", 100)),
            Fingerprints("/lib/b.unitypackage", ("Assets/B.png", "HASH-B", 100))
        ]);

        Assert.Empty(report.Groups);
        Assert.False(report.HasFindings);
        Assert.Equal(0, report.TotalWastedBytes);
    }

    [Theory]
    [InlineData("guid123/asset", "guid123", "asset")]
    [InlineData("guid123/pathname", "guid123", "pathname")]
    [InlineData("guid123/asset.meta", "guid123", "asset.meta")]
    [InlineData("guid123\\asset", "guid123", "asset")]
    [InlineData("guid123/ASSET", "guid123", "asset")]
    [InlineData("noslash", "", "")]
    [InlineData("", "", "")]
    public void SplitEntry_SeparatesGuidFromComponent(string entryName, string expectedKey, string expectedComponent)
    {
        var (key, component) = DuplicateAssetScanner.SplitEntry(entryName);

        Assert.Equal(expectedKey, key);
        Assert.Equal(expectedComponent, component);
    }

    private static DuplicateAssetScanner CreateScanner()
    {
        return new DuplicateAssetScanner(new RecordingLogger());
    }

    private static PackageFingerprintResult Fingerprints(
        string packagePath,
        params (string AssetPath, string Hash, long Size)[] assets)
    {
        return new PackageFingerprintResult(
            packagePath,
            assets.Select(asset => new PackageAssetFingerprint(asset.AssetPath, asset.Hash, asset.Size)).ToList());
    }
}
