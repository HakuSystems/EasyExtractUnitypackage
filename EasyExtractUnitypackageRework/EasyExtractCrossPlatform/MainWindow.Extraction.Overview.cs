namespace EasyExtractCrossPlatform;

public partial class MainWindow : Window
{
    private void BeginExtractionOverviewSession()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(BeginExtractionOverviewSession);
            return;
        }

        _extractionOverviewPackageBaseline = Math.Max(0, _settings.TotalExtracted);
        _extractionOverviewAssetBaseline = Math.Max(0, _settings.TotalFilesExtracted);
        _extractionOverviewSessionPackages = 0;
        _extractionOverviewSessionAssets = 0;
        _extractionOverviewCurrentPackageAssets = 0;
        _extractionOverviewStartTime = DateTimeOffset.Now;
        _isExtractionOverviewLive = true;
        UpdateExtractionOverviewStatusInProgress();
        UpdateExtractionOverviewLiveCounts();
    }


    private void OnExtractionOverviewPackageStarted()
    {
        if (!_isExtractionOverviewLive)
            return;

        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(OnExtractionOverviewPackageStarted);
            return;
        }

        _extractionOverviewCurrentPackageAssets = 0;
        UpdateExtractionOverviewLiveCounts();
    }


    private void UpdateExtractionOverviewProgress(int assetsExtracted)
    {
        if (!_isExtractionOverviewLive)
            return;

        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => UpdateExtractionOverviewProgress(assetsExtracted));
            return;
        }

        var sanitized = Math.Max(0, assetsExtracted);
        if (sanitized <= _extractionOverviewCurrentPackageAssets)
            return;

        var delta = sanitized - _extractionOverviewCurrentPackageAssets;
        _extractionOverviewCurrentPackageAssets = sanitized;
        _extractionOverviewSessionAssets += delta;
        UpdateExtractionOverviewLiveCounts();
    }


    private void OnExtractionOverviewPackageCompleted(int assetsExtracted)
    {
        if (!_isExtractionOverviewLive)
            return;

        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => OnExtractionOverviewPackageCompleted(assetsExtracted));
            return;
        }

        var sanitized = Math.Max(0, assetsExtracted);
        if (sanitized > _extractionOverviewCurrentPackageAssets)
        {
            _extractionOverviewSessionAssets += sanitized - _extractionOverviewCurrentPackageAssets;
            _extractionOverviewCurrentPackageAssets = sanitized;
        }

        _extractionOverviewSessionPackages++;
        UpdateExtractionOverviewLiveCounts();
    }


    private void UpdateExtractionOverviewLiveCounts()
    {
        if (!_isExtractionOverviewLive)
            return;

        var packages = _extractionOverviewPackageBaseline + _extractionOverviewSessionPackages;
        var assets = _extractionOverviewAssetBaseline + _extractionOverviewSessionAssets;
        UpdateExtractionOverviewCounts(packages, assets);
    }


    private void UpdateExtractionOverviewCounts(int packages, int assets)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => UpdateExtractionOverviewCounts(packages, assets));
            return;
        }

        if (_extractionOverviewPackagesCompletedText is not null)
            _extractionOverviewPackagesCompletedText.Text = packages.ToString("N0", CultureInfo.CurrentCulture);

        if (_extractionOverviewAssetsExtractedText is not null)
            _extractionOverviewAssetsExtractedText.Text = assets.ToString("N0", CultureInfo.CurrentCulture);
    }


    private void UpdateExtractionOverviewStatus(string text)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => UpdateExtractionOverviewStatus(text));
            return;
        }

        if (_extractionOverviewLastExtractionText is not null)
            _extractionOverviewLastExtractionText.Text = text;
    }


    private void UpdateExtractionOverviewStatusInProgress()
    {
        var statusText = _extractionOverviewStartTime.HasValue
            ? $"Started at {_extractionOverviewStartTime.Value.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)}"
            : "In progress�";

        UpdateExtractionOverviewStatus(statusText);
    }


    private void UpdateExtractionOverviewDisplay()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(UpdateExtractionOverviewDisplay);
            return;
        }

        var packages = Math.Max(0, _settings.TotalExtracted);
        var assets = Math.Max(0, _settings.TotalFilesExtracted);
        UpdateExtractionOverviewCounts(packages, assets);

        if (_extractionOverviewLastExtractionText is null)
            return;

        _extractionOverviewLastExtractionText.Text = _settings.LastExtractionTime.HasValue
            ? _settings.LastExtractionTime.Value.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)
            : "Not started";
    }


    private void EndExtractionOverviewSession()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(EndExtractionOverviewSession);
            return;
        }

        _isExtractionOverviewLive = false;
        _extractionOverviewSessionPackages = 0;
        _extractionOverviewSessionAssets = 0;
        _extractionOverviewCurrentPackageAssets = 0;
        _extractionOverviewStartTime = null;
        UpdateExtractionOverviewDisplay();
    }
}