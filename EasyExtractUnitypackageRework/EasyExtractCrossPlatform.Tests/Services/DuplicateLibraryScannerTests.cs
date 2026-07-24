using EasyExtract.Core.Models;
using EasyExtract.Core.Services;
using EasyExtractCrossPlatform.Services;
using EasyExtractCrossPlatform.Tests.Support;
using Xunit;

namespace EasyExtractCrossPlatform.Tests.Services;

public sealed class DuplicateLibraryScannerTests : IDisposable
{
    private readonly string _cachePath;
    private readonly string _library;
    private readonly string _root;

    public DuplicateLibraryScannerTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "EasyExtractTests", "Library", Guid.NewGuid().ToString("N"));
        _library = Path.Combine(_root, "library");
        _cachePath = Path.Combine(_root, "cache", "duplicate-index.json");
        Directory.CreateDirectory(_library);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, true);
    }

    [Fact]
    public async Task ScanAsync_FindsTheSameAssetInTwoPackages()
    {
        const string shared = "the shared shader body";
        await new UnityPackageBuilder()
            .WithAsset("Assets/Poiyomi/Poiyomi.shader", shared)
            .WithAsset("Assets/Only/In/First.txt", "unique to first")
            .WriteAsync(_library, "first.unitypackage");
        await new UnityPackageBuilder()
            .WithAsset("Assets/Vendor/Poiyomi.shader", shared)
            .WriteAsync(_library, "second.unitypackage");

        var scanner = CreateScanner();
        var report = await scanner.ScanAsync(scanner.FindPackages(_library));

        Assert.Equal(2, report.PackagesScanned);
        var group = Assert.Single(report.Groups);
        Assert.True(group.SpansMultiplePackages);
        Assert.Equal(2, group.Occurrences.Count);
        Assert.Contains(group.Occurrences, o => o.PackageName == "first.unitypackage");
        Assert.Contains(group.Occurrences, o => o.PackageName == "second.unitypackage");
    }

    [Fact]
    public async Task ScanAsync_ReportsProgressForEveryPackage()
    {
        await new UnityPackageBuilder().WithAsset("Assets/A.txt", "a").WriteAsync(_library, "a.unitypackage");
        await new UnityPackageBuilder().WithAsset("Assets/B.txt", "b").WriteAsync(_library, "b.unitypackage");

        var updates = new List<DuplicateScanProgress>();
        var scanner = CreateScanner();

        await scanner.ScanAsync(scanner.FindPackages(_library), new Progress<DuplicateScanProgress>(updates.Add));

        // Progress is posted through the synchronization context, so allow it to drain.
        for (var attempt = 0; attempt < 50 && updates.Count < 3; attempt++)
            await Task.Delay(10);

        Assert.NotEmpty(updates);
        Assert.All(updates, update => Assert.Equal(2, update.PackagesTotal));
        Assert.Equal(2, updates[^1].PackagesCompleted);
        Assert.Equal(1d, updates[^1].Fraction);
    }

    [Fact]
    public async Task ScanAsync_SecondRunReusesTheCacheInsteadOfRehashing()
    {
        await new UnityPackageBuilder().WithAsset("Assets/A.txt", "a").WriteAsync(_library, "a.unitypackage");

        var counting = new CountingScanner();
        var scanner = new DuplicateLibraryScanner(counting, _cachePath);
        var packages = scanner.FindPackages(_library);

        await scanner.ScanAsync(packages);
        Assert.Equal(1, counting.Calls);

        await scanner.ScanAsync(packages);
        Assert.Equal(1, counting.Calls);
    }

    [Fact]
    public async Task ScanAsync_RehashesAPackageThatChangedOnDisk()
    {
        var package = await new UnityPackageBuilder()
            .WithAsset("Assets/A.txt", "a")
            .WriteAsync(_library, "a.unitypackage");

        var counting = new CountingScanner();
        var scanner = new DuplicateLibraryScanner(counting, _cachePath);

        await scanner.ScanAsync([package]);
        Assert.Equal(1, counting.Calls);

        await new UnityPackageBuilder()
            .WithAsset("Assets/A.txt", "a")
            .WithAsset("Assets/B.txt", "b")
            .WriteAsync(_library, "a.unitypackage");
        File.SetLastWriteTimeUtc(package, DateTime.UtcNow.AddMinutes(5));

        await scanner.ScanAsync([package]);
        Assert.Equal(2, counting.Calls);
    }

    [Fact]
    public async Task ScanAsync_KeepsGoingWhenOnePackageIsBroken()
    {
        await new UnityPackageBuilder().WithAsset("Assets/A.txt", "a").WriteAsync(_library, "good.unitypackage");
        await File.WriteAllTextAsync(Path.Combine(_library, "broken.unitypackage"), "not a package at all");

        var scanner = CreateScanner();
        var report = await scanner.ScanAsync(scanner.FindPackages(_library));

        Assert.Equal(1, report.PackagesScanned);
        Assert.Equal(1, report.PackagesFailed);
        Assert.EndsWith("broken.unitypackage", Assert.Single(report.FailedPackages));
    }

    [Fact]
    public async Task ScanAsync_IsCancellable()
    {
        await new UnityPackageBuilder().WithAsset("Assets/A.txt", "a").WriteAsync(_library, "a.unitypackage");

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var scanner = CreateScanner();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => scanner.ScanAsync(scanner.FindPackages(_library), null, cts.Token));
    }

    [Fact]
    public async Task ScanAsync_WithoutPackagesReturnsAnEmptyReport()
    {
        var report = await CreateScanner().ScanAsync([]);

        Assert.False(report.HasFindings);
        Assert.Equal(0, report.PackagesScanned);
    }

    [Fact]
    public void Normalize_RemovesDuplicatesAndBlanks()
    {
        var normalized = DuplicateLibraryScanner.Normalize(
        [
            Path.Combine(_library, "a.unitypackage"),
            Path.Combine(_library, "a.unitypackage"),
            "   ",
            string.Empty
        ]);

        Assert.Single(normalized);
    }

    [Fact]
    public async Task FindPackages_OnlyReturnsUnityPackages()
    {
        await new UnityPackageBuilder().WithAsset("Assets/A.txt", "a").WriteAsync(_library, "a.unitypackage");
        await File.WriteAllTextAsync(Path.Combine(_library, "notes.txt"), "ignore me");

        var found = CreateScanner().FindPackages(_library);

        Assert.EndsWith("a.unitypackage", Assert.Single(found));
    }

    [Fact]
    public void FindPackages_ReturnsNothingForAMissingDirectory()
    {
        Assert.Empty(CreateScanner().FindPackages(Path.Combine(_root, "nope")));
    }

    private DuplicateLibraryScanner CreateScanner()
    {
        return new DuplicateLibraryScanner(new DuplicateAssetScanner(new RecordingLogger()), _cachePath);
    }

    private sealed class CountingScanner : IDuplicateAssetScanner
    {
        private readonly DuplicateAssetScanner _inner = new(new RecordingLogger());

        public int Calls { get; private set; }

        public Task<PackageFingerprintResult> FingerprintPackageAsync(
            string packagePath,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return _inner.FingerprintPackageAsync(packagePath, cancellationToken);
        }
    }
}
