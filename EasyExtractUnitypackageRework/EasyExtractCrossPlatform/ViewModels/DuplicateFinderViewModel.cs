using CommunityToolkit.Mvvm.Input;

namespace EasyExtractCrossPlatform.ViewModels;

/// <summary>
///     Drives the duplicate finder window: choose a library folder, hash every package in it and
///     show which assets are stored more than once.
/// </summary>
public sealed class DuplicateFinderViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly IDuplicateLibraryScanner _scanner;
    private CancellationTokenSource? _activeScanCts;
    private bool _disposed;
    private bool _isScanning;
    private string? _libraryPath;
    private double _progressFraction;
    private string _progressText = string.Empty;
    private DuplicateScanReport _report = DuplicateScanReport.Empty;
    private string? _statusMessage;

    public DuplicateFinderViewModel(IDuplicateLibraryScanner scanner)
    {
        _scanner = scanner ?? throw new ArgumentNullException(nameof(scanner));

        CancelCommand = new RelayCommand(CancelScan, () => IsScanning);
    }

    public ObservableCollection<DuplicateGroupDisplay> Groups { get; } = new();

    public RelayCommand CancelCommand { get; }

    public string? LibraryPath
    {
        get => _libraryPath;
        private set
        {
            if (string.Equals(_libraryPath, value, StringComparison.Ordinal))
                return;

            _libraryPath = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasLibraryPath));
            OnPropertyChanged(nameof(ShowEmptyState));
            OnPropertyChanged(nameof(SummaryDetail));
        }
    }

    public bool HasLibraryPath => !string.IsNullOrWhiteSpace(LibraryPath);

    public bool IsScanning
    {
        get => _isScanning;
        private set
        {
            if (_isScanning == value)
                return;

            _isScanning = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsIdle));
            // The empty state depends on this, otherwise "nothing found" shows while still scanning.
            OnPropertyChanged(nameof(ShowEmptyState));
            CancelCommand.NotifyCanExecuteChanged();
        }
    }

    public bool IsIdle => !IsScanning;

    public double ProgressFraction
    {
        get => _progressFraction;
        private set
        {
            if (Math.Abs(_progressFraction - value) < 0.0001)
                return;

            _progressFraction = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ProgressPercent));
        }
    }

    public double ProgressPercent => ProgressFraction * 100d;

    public string ProgressText
    {
        get => _progressText;
        private set
        {
            if (string.Equals(_progressText, value, StringComparison.Ordinal))
                return;

            _progressText = value;
            OnPropertyChanged();
        }
    }

    public string? StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (string.Equals(_statusMessage, value, StringComparison.Ordinal))
                return;

            _statusMessage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasStatusMessage));
        }
    }

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    public DuplicateScanReport Report
    {
        get => _report;
        private set
        {
            _report = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasFindings));
            OnPropertyChanged(nameof(ShowEmptyState));
            OnPropertyChanged(nameof(SummaryHeadline));
            OnPropertyChanged(nameof(SummaryDetail));
        }
    }

    public bool HasFindings => Report.HasFindings;

    public bool ShowEmptyState => !IsScanning && HasLibraryPath && !HasFindings;

    public string SummaryHeadline => Report.HasFindings
        ? $"{FormatBytes(Report.TotalWastedBytes)} stored more than once"
        : "No duplicated assets";

    public string SummaryDetail
    {
        get
        {
            if (!HasLibraryPath)
                return "Pick a folder that holds your .unitypackage files.";

            var detail =
                $"{Report.TotalDuplicateCopies:N0} extra copies across {Report.Groups.Count:N0} assets in " +
                $"{Report.PackagesScanned:N0} packages.";

            return Report.PackagesFailed > 0
                ? $"{detail} {Report.PackagesFailed:N0} package(s) could not be read."
                : detail;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        CancelScan();
        _activeScanCts?.Dispose();
        _activeScanCts = null;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public async Task ScanAsync(string libraryPath)
    {
        if (string.IsNullOrWhiteSpace(libraryPath) || IsScanning)
            return;

        LibraryPath = libraryPath;
        StatusMessage = null;
        Groups.Clear();
        Report = DuplicateScanReport.Empty;

        var packages = _scanner.FindPackages(libraryPath);
        if (packages.Count == 0)
        {
            StatusMessage = "No .unitypackage files were found in that folder.";
            OnPropertyChanged(nameof(ShowEmptyState));
            return;
        }

        CancelScan();
        var cts = new CancellationTokenSource();
        _activeScanCts = cts;

        IsScanning = true;
        ProgressFraction = 0;
        ProgressText = $"Preparing {packages.Count:N0} packages...";

        try
        {
            var progress = new Progress<DuplicateScanProgress>(update =>
            {
                ProgressFraction = update.Fraction;
                ProgressText = string.IsNullOrEmpty(update.CurrentPackageName)
                    ? $"{update.PackagesCompleted:N0} of {update.PackagesTotal:N0} packages"
                    : $"{update.PackagesCompleted:N0} of {update.PackagesTotal:N0} · {update.CurrentPackageName}";
            });

            var report = await _scanner.ScanAsync(packages, progress, cts.Token).ConfigureAwait(true);

            if (cts.IsCancellationRequested)
                return;

            Report = report;
            foreach (var group in report.Groups)
                Groups.Add(new DuplicateGroupDisplay(group, libraryPath));

            ProgressText = string.Empty;
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Scan cancelled.";
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Duplicate scan failed.", ex);
            StatusMessage = $"Scan failed: {ex.Message}";
        }
        finally
        {
            if (ReferenceEquals(_activeScanCts, cts))
            {
                _activeScanCts = null;
                IsScanning = false;
            }

            // Each run owns its source, so a superseded run still releases it.
            cts.Dispose();
            OnPropertyChanged(nameof(ShowEmptyState));
        }
    }

    public void CancelScan()
    {
        var cts = _activeScanCts;
        if (cts is null || cts.IsCancellationRequested)
            return;

        try
        {
            cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The scan finished between the null check and the cancel; nothing to do.
        }
    }

    internal static string FormatBytes(long bytes)
    {
        if (bytes <= 0)
            return "0 B";

        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        var unit = 0;

        while (value >= 1024d && unit < units.Length - 1)
        {
            value /= 1024d;
            unit++;
        }

        return unit == 0
            ? $"{value:0} {units[unit]}"
            : $"{value:0.##} {units[unit]}";
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
///     A duplicate group prepared for display.
/// </summary>
public sealed class DuplicateGroupDisplay
{
    public DuplicateGroupDisplay(DuplicateAssetGroup group, string? libraryRoot = null)
    {
        ArgumentNullException.ThrowIfNull(group);

        AssetName = Path.GetFileName(group.DisplayName) is { Length: > 0 } name ? name : group.DisplayName;
        AssetPath = group.DisplayName;
        WastedText = DuplicateFinderViewModel.FormatBytes(group.WastedBytes);
        SizeText = DuplicateFinderViewModel.FormatBytes(group.SizeBytes);
        CopiesText = $"{group.Occurrences.Count:N0} copies";
        ScopeText = group.SpansMultiplePackages
            ? $"in {group.DistinctPackageCount:N0} packages"
            : "inside one package";
        // Libraries routinely hold the same package file name in several folders, sometimes even
        // under equally named parents. Showing the bare name twice reads like a rendering bug, so
        // ambiguous entries fall back to their path relative to the scanned folder.
        var ambiguousNames = group.Occurrences
            .GroupBy(occurrence => occurrence.PackageName, StringComparer.OrdinalIgnoreCase)
            .Where(entries => entries
                .Select(occurrence => occurrence.PackagePath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() > 1)
            .Select(entries => entries.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Occurrences = group.Occurrences
            .Select(occurrence => new DuplicateOccurrenceDisplay(
                occurrence,
                ambiguousNames.Contains(occurrence.PackageName),
                libraryRoot))
            .ToList();
    }

    public string AssetName { get; }
    public string AssetPath { get; }
    public string WastedText { get; }
    public string SizeText { get; }
    public string CopiesText { get; }
    public string ScopeText { get; }
    public IReadOnlyList<DuplicateOccurrenceDisplay> Occurrences { get; }
}

public sealed class DuplicateOccurrenceDisplay
{
    public DuplicateOccurrenceDisplay(
        DuplicateAssetOccurrence occurrence,
        bool disambiguate = false,
        string? libraryRoot = null)
    {
        ArgumentNullException.ThrowIfNull(occurrence);

        PackageName = occurrence.PackageName;
        PackagePath = occurrence.PackagePath;
        AssetPath = occurrence.AssetPath;
        PackageLabel = BuildLabel(occurrence, disambiguate, libraryRoot);
    }

    public string PackageName { get; }
    public string PackagePath { get; }
    public string AssetPath { get; }

    /// <summary>The package name, prefixed with its folder when the name alone is ambiguous.</summary>
    public string PackageLabel { get; }

    internal static string BuildLabel(DuplicateAssetOccurrence occurrence, bool disambiguate, string? libraryRoot)
    {
        if (!disambiguate)
            return occurrence.PackageName;

        if (!string.IsNullOrWhiteSpace(libraryRoot))
            try
            {
                var relative = Path.GetRelativePath(libraryRoot, occurrence.PackagePath).Replace('\\', '/');
                if (!relative.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(relative))
                    return relative;
            }
            catch (Exception)
            {
                // Fall through to the folder based label below.
            }

        var folder = Path.GetFileName(Path.GetDirectoryName(occurrence.PackagePath));
        return string.IsNullOrWhiteSpace(folder)
            ? occurrence.PackageName
            : $"{folder}/{occurrence.PackageName}";
    }
}
