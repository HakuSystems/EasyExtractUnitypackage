using System.Globalization;
using EasyExtract.Core.Models;
using EasyExtractCrossPlatform.Services;
using EasyExtractCrossPlatform.ViewModels;
using Xunit;

namespace EasyExtractCrossPlatform.Tests.ViewModels;

public sealed class DuplicateFinderViewModelTests
{
    [Fact]
    public async Task ScanAsync_PublishesGroupsAndSummary()
    {
        var scanner = new StubScanner
        {
            Packages = ["/lib/a.unitypackage", "/lib/b.unitypackage"],
            Report = ReportWith(
                Group("HASH", 2048, ("/lib/a.unitypackage", "Assets/Shared.png"),
                    ("/lib/b.unitypackage", "Assets/Copy/Shared.png")))
        };

        var viewModel = new DuplicateFinderViewModel(scanner);
        await viewModel.ScanAsync("/lib");

        var group = Assert.Single(viewModel.Groups);
        Assert.Equal("Shared.png", group.AssetName);
        Assert.Equal("2 KB", group.WastedText);
        Assert.Equal("2 copies", group.CopiesText);
        Assert.Equal("in 2 packages", group.ScopeText);
        Assert.Equal(2, group.Occurrences.Count);
        Assert.True(viewModel.HasFindings);
        Assert.Contains("2 KB", viewModel.SummaryHeadline);
        Assert.False(viewModel.IsScanning);
    }

    [Fact]
    public async Task ScanAsync_WithoutPackagesExplainsWhyNothingHappened()
    {
        var viewModel = new DuplicateFinderViewModel(new StubScanner());

        await viewModel.ScanAsync("/empty");

        Assert.True(viewModel.HasStatusMessage);
        Assert.Contains("No .unitypackage files", viewModel.StatusMessage);
        Assert.False(viewModel.IsScanning);
        Assert.Empty(viewModel.Groups);
    }

    [Fact]
    public async Task ScanAsync_ClearsPreviousResultsBeforeRescanning()
    {
        var scanner = new StubScanner
        {
            Packages = ["/lib/a.unitypackage"],
            Report = ReportWith(Group("HASH", 100, ("/lib/a.unitypackage", "Assets/A.png"),
                ("/lib/a.unitypackage", "Assets/B.png")))
        };

        var viewModel = new DuplicateFinderViewModel(scanner);
        await viewModel.ScanAsync("/lib");
        Assert.Single(viewModel.Groups);

        scanner.Report = DuplicateScanReport.Empty;
        await viewModel.ScanAsync("/lib");

        Assert.Empty(viewModel.Groups);
        Assert.True(viewModel.ShowEmptyState);
    }

    [Fact]
    public async Task ScanAsync_SurfacesFailuresInsteadOfThrowing()
    {
        var scanner = new StubScanner
        {
            Packages = ["/lib/a.unitypackage"],
            Failure = new InvalidOperationException("disk went away")
        };

        var viewModel = new DuplicateFinderViewModel(scanner);
        await viewModel.ScanAsync("/lib");

        Assert.Contains("disk went away", viewModel.StatusMessage);
        Assert.False(viewModel.IsScanning);
    }

    [Fact]
    public async Task ScanAsync_ReportsCancellation()
    {
        var scanner = new StubScanner
        {
            Packages = ["/lib/a.unitypackage"],
            Failure = new OperationCanceledException()
        };

        var viewModel = new DuplicateFinderViewModel(scanner);
        await viewModel.ScanAsync("/lib");

        Assert.Equal("Scan cancelled.", viewModel.StatusMessage);
        Assert.False(viewModel.IsScanning);
    }

    [Fact]
    public async Task ScanAsync_CountsFailedPackagesInTheSummary()
    {
        var scanner = new StubScanner
        {
            Packages = ["/lib/a.unitypackage", "/lib/broken.unitypackage"],
            Report = new DuplicateScanReport([], 1, 1, ["/lib/broken.unitypackage"])
        };

        var viewModel = new DuplicateFinderViewModel(scanner);
        await viewModel.ScanAsync("/lib");

        Assert.Contains("1 package(s) could not be read", viewModel.SummaryDetail);
    }

    [Fact]
    public void EmptyStateStaysHiddenWhileAScanIsRunning()
    {
        var gate = new TaskCompletionSource<DuplicateScanReport>();
        var scanner = new StubScanner
        {
            Packages = ["/lib/a.unitypackage"],
            Gate = gate.Task
        };

        var viewModel = new DuplicateFinderViewModel(scanner);
        var scan = viewModel.ScanAsync("/lib");

        Assert.True(viewModel.IsScanning);
        Assert.False(viewModel.ShowEmptyState);

        gate.SetResult(DuplicateScanReport.Empty);
        scan.GetAwaiter().GetResult();

        Assert.False(viewModel.IsScanning);
        Assert.True(viewModel.ShowEmptyState);
    }

    /// <summary>
    ///     Seen in a real library: the same package name under two different parents that were both
    ///     called "packages", so one folder level was not enough to tell the rows apart.
    /// </summary>
    [Fact]
    public async Task Occurrences_DisambiguateWithThePathRelativeToTheScannedFolder()
    {
        var root = Path.Combine(Path.GetTempPath(), "lib");
        var first = Path.Combine(root, "Classified", "packages", "pack.unitypackage");
        var second = Path.Combine(root, "Backup", "Classified Group", "packages", "pack.unitypackage");

        var scanner = new StubScanner
        {
            Packages = [first, second],
            Report = ReportWith(Group("HASH", 100,
                (first, "Assets/Shared.png"),
                (second, "Assets/Shared.png")))
        };

        var viewModel = new DuplicateFinderViewModel(scanner);
        await viewModel.ScanAsync(root);

        var labels = Assert.Single(viewModel.Groups).Occurrences.Select(o => o.PackageLabel).ToList();
        Assert.Contains("Classified/packages/pack.unitypackage", labels);
        Assert.Contains("Backup/Classified Group/packages/pack.unitypackage", labels);
        Assert.Equal(2, labels.Distinct().Count());
    }

    [Fact]
    public void BuildLabel_FallsBackToTheParentFolderWithoutARoot()
    {
        var occurrence = new DuplicateAssetOccurrence(
            Path.Combine("some", "where", "pack.unitypackage"), "Assets/A.png");

        Assert.Equal("where/pack.unitypackage",
            DuplicateOccurrenceDisplay.BuildLabel(occurrence, true, null));
    }

    [Fact]
    public void BuildLabel_FallsBackWhenThePackageSitsOutsideTheRoot()
    {
        var occurrence = new DuplicateAssetOccurrence(
            Path.Combine(Path.GetTempPath(), "elsewhere", "pack.unitypackage"), "Assets/A.png");

        var label = DuplicateOccurrenceDisplay.BuildLabel(
            occurrence, true, Path.Combine(Path.GetTempPath(), "lib"));

        Assert.Equal("elsewhere/pack.unitypackage", label);
    }

    [Fact]
    public async Task Occurrences_KeepPlainNamesWhenTheyAreAlreadyUnique()
    {
        var scanner = new StubScanner
        {
            Packages = ["/lib/first.unitypackage", "/lib/second.unitypackage"],
            Report = ReportWith(Group("HASH", 100,
                ("/lib/first.unitypackage", "Assets/Shared.png"),
                ("/lib/second.unitypackage", "Assets/Shared.png")))
        };

        var viewModel = new DuplicateFinderViewModel(scanner);
        await viewModel.ScanAsync("/lib");

        var labels = Assert.Single(viewModel.Groups).Occurrences.Select(o => o.PackageLabel).ToList();
        Assert.Contains("first.unitypackage", labels);
        Assert.Contains("second.unitypackage", labels);
    }

    /// <summary>
    ///     Sizes are shown in the user's locale, so a German machine prints "1,5 KB". The test pins
    ///     the culture instead of the separator, otherwise it would pass in CI and fail on a dev box.
    /// </summary>
    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(512, "512 B")]
    [InlineData(1024, "1 KB")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(1048576, "1 MB")]
    [InlineData(3221225472, "3 GB")]
    public void FormatBytes_IsHumanReadable(long bytes, string expected)
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

        try
        {
            Assert.Equal(expected, DuplicateFinderViewModel.FormatBytes(bytes));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void FormatBytes_FollowsTheUsersDecimalSeparator()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");

        try
        {
            Assert.Equal("1,5 KB", DuplicateFinderViewModel.FormatBytes(1536));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    private static DuplicateScanReport ReportWith(params DuplicateAssetGroup[] groups)
    {
        return new DuplicateScanReport(groups, groups.Length, 0, []);
    }

    private static DuplicateAssetGroup Group(string hash, long size,
        params (string PackagePath, string AssetPath)[] occurrences)
    {
        return new DuplicateAssetGroup(hash, size,
            occurrences.Select(o => new DuplicateAssetOccurrence(o.PackagePath, o.AssetPath)).ToList());
    }

    private sealed class StubScanner : IDuplicateLibraryScanner
    {
        public IReadOnlyList<string> Packages { get; init; } = [];
        public DuplicateScanReport Report { get; set; } = DuplicateScanReport.Empty;
        public Exception? Failure { get; init; }
        public Task<DuplicateScanReport>? Gate { get; init; }

        public Task<DuplicateScanReport> ScanAsync(
            IReadOnlyList<string> packagePaths,
            IProgress<DuplicateScanProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (Failure is not null)
                return Task.FromException<DuplicateScanReport>(Failure);

            progress?.Report(new DuplicateScanProgress(packagePaths.Count, packagePaths.Count, string.Empty));
            return Gate ?? Task.FromResult(Report);
        }

        public IReadOnlyList<string> FindPackages(string directory, bool includeSubdirectories = true)
        {
            return Packages;
        }
    }
}
