using System.Windows.Data;
using EasyExtract.BetterExtraction;
using EasyExtract.Config;
using EasyExtract.Config.Models;
using EasyExtract.Services;
using EasyExtract.Utilities;
using EasyExtract.Views;

namespace EasyExtract.Controls;

public partial class BetterExtraction
{
    private const string QueueFilesKey = "QueueFiles";

    private readonly FileProgress _currentFileProgress = new();
    private bool _hasCheckedSystemRequirements;

    private int _recheckCount;

    // cache the system requirements result
    private bool? _systemRequirementsMet;

    public BetterExtraction()
    {
        InitializeComponent();
        DataContext = ConfigHandler.Instance.Config;
    }

    private LocateUnitypackage UnitypackageLocator { get; } = new();
    private EverythingValidation EverythingValidation { get; } = new();
    private HashChecks HashChecks { get; } = new();
    private FilterQueue FilterQueue { get; } = new();
    private ExtractionHandler ExtractionHandler { get; } = new();

    private async void LocateUnitypackageButton_OnClick(object sender, RoutedEventArgs e)
    {
        var result = await UnitypackageLocator.LocateUnitypackageFilesAsync();
        if (result != null) SyncFileCollections();
    }

    private void SyncFileCollections()
    {
        if (Resources[QueueFilesKey] is CollectionViewSource queueFiles)
            queueFiles.View.Refresh();
        else
            throw new InvalidOperationException($"Resource '{QueueFilesKey}' not found.");

        UpdateViewExtractionButtonVisibility();
        UpdateStartExtractionButtonVisibility();
        UpdateClearQueueButtonVisibility();
    }

    private void UpdateStartExtractionButtonVisibility()
    {
        StartExtractionButton.Visibility = ConfigHandler.Instance.Config.UnitypackageFiles.Any(file => file.IsInQueue)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void UpdateViewExtractionButtonVisibility()
    {
        var appDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "EasyExtract");

        if (!Directory.Exists(appDataFolder))
            Directory.CreateDirectory(appDataFolder);

        var extractionFolder = Path.Combine(appDataFolder, "Extracted");

        var folderExists = Directory.Exists(extractionFolder) &&
                           Directory.EnumerateFileSystemEntries(extractionFolder).Any();

        Application.Current.Dispatcher.Invoke(() =>
        {
            ViewExtractionButton.Visibility = folderExists
                ? Visibility.Visible
                : Visibility.Collapsed;
        });
    }


    private void UpdateClearQueueButtonVisibility()
    {
        ClearQueueButton.Visibility = ConfigHandler.Instance.Config.UnitypackageFiles.Any(file => file.IsInQueue)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void SetupFilter(string resourceKey, Func<object, bool> filterPredicate)
    {
        var cvs = Resources[resourceKey] as CollectionViewSource
                  ?? throw new InvalidOperationException($"Resource '{resourceKey}' not found.");
        cvs.Filter += (sender, args) => { args.Accepted = filterPredicate(args.Item); };
    }

    private async void BetterExtraction_OnLoaded(object sender, RoutedEventArgs e)
    {
        Dashboard.Instance.NavigateBackBtn.Visibility = Visibility.Hidden;

        await DiscordRpcManager.Instance.TryUpdatePresenceAsync("Extraction");
        ConfigHandler.Instance.Config.SearchEverythingResults.Clear();
        ConfigHandler.Instance.OverrideConfig();
        SetupFilter(QueueFilesKey, item => item is UnitypackageFileInfo file && file.IsInQueue);

        if (Resources["SearchResults"] is CollectionViewSource searchResults)
            searchResults.Filter += (s, args) =>
            {
                if (args.Item is SearchEverythingModel model)
                {
                    var searchText = SearchUnitypackageBoxInput.Text;
                    args.Accepted = string.IsNullOrWhiteSpace(searchText) ||
                                    model.FileName.IndexOf(searchText, StringComparison.InvariantCultureIgnoreCase) >=
                                    0;
                }
                else
                {
                    args.Accepted = false;
                }
            };
        else
            throw new InvalidOperationException("Resource 'SearchResults' not found.");

        SyncFileCollections();
    }

    private void PopulateSearchResultsAsync()
    {
        var resultCount = Everything.Everything_GetNumResults();
        var addedNewItem = false;
        for (uint i = 0; i < resultCount; i++)
        {
            var path = Everything.GetResultFullPathName(i);
            var name = Marshal.PtrToStringUni(Everything.Everything_GetResultFileName(i));
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(path))
                continue;
            if (!File.Exists(path))
                continue;
            if (ConfigHandler.Instance.Config.SearchEverythingResults.Exists(x =>
                    x.FileName.Equals(name, StringComparison.InvariantCultureIgnoreCase)))
                continue;
            var model = new SearchEverythingModel
            {
                FileName = name,
                FilePath = path,
                Id = i,
                FileSize = SearchEverythingFileChecks.GetFileSize(path),
                ModifiedTime = SearchEverythingFileChecks.GetFileDateTime(i, false),
                CreatedTime = SearchEverythingFileChecks.GetFileDateTime(i, true)
            };
            ConfigHandler.Instance.Config.SearchEverythingResults.Add(model);
            addedNewItem = true;
        }

        if (addedNewItem)
            ConfigHandler.Instance.OverrideConfig();
    }

    private async Task<bool> CheckSystemRequirementsAndUpdateUiAsync(bool forceCheck)
    {
        if (!forceCheck && _systemRequirementsMet.HasValue)
            return _systemRequirementsMet.Value;

        _recheckCount = forceCheck ? _recheckCount + 1 : 0;

        var requirementsMet = await Task.Run(() => EverythingValidation.AreSystemRequirementsMetAsync());
        _systemRequirementsMet = requirementsMet;

        Application.Current.Dispatcher.Invoke(() =>
        {
            SearchUnitypackageBox.Visibility = requirementsMet ? Visibility.Visible : Visibility.Collapsed;
            SearchUnitypackageBoxExpanderError.Visibility = requirementsMet ? Visibility.Collapsed : Visibility.Visible;
        });

        if (!requirementsMet)
        {
            var statusMessage = await Task.Run(() => EverythingValidation.GetSystemRequirementsStatusAsync());
            statusMessage = forceCheck ? $"Re-check (attempt #{_recheckCount}): {statusMessage}" : statusMessage;

            Application.Current.Dispatcher.Invoke(() => { SearchUnitypackageBoxFallback.Text = statusMessage; });

            var logMessage = forceCheck
                ? $"System requirements still not met after attempt #{_recheckCount}."
                : "System requirements not met initially.";
            await BetterLogger.LogAsync(logMessage, Importance.Warning);
        }
        else if (forceCheck)
        {
            await BetterLogger.LogAsync("System requirements met after re-check.", Importance.Info);
            await Task.Run(() =>
            {
                Everything.Everything_SetSearchW("endwith:.unitypackage !C:\\$Recycle.Bin\\*");
                Everything.Everything_SetRequestFlags(Everything.RequestFileName | Everything.RequestPath);
                Everything.Everything_QueryW(true);
                Task.Run(PopulateSearchResultsAsync);
            });
        }

        return requirementsMet;
    }

    private async void SearchUnitypackageBoxFallbackButton_OnClick(object sender, RoutedEventArgs e)
    {
        _systemRequirementsMet = null;
        await CheckSystemRequirementsAndUpdateUiAsync(true);
    }

    private void ClearQueueButton_OnClick(object sender, RoutedEventArgs e)
    {
        foreach (var unitypackageFile in ConfigHandler.Instance.Config.UnitypackageFiles
                     .Where(u => u.IsInQueue)
                     .ToList()) // ToList() to avoid modifying the collection while iterating
            ConfigHandler.Instance.Config.UnitypackageFiles.Remove(unitypackageFile);

        ConfigHandler.Instance.OverrideConfig();
        SyncFileCollections();
    }

    private async void SearchUnitypackageBoxInput_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        await ExpandSearchBoxAndRefreshAsync(true);
        if (string.IsNullOrWhiteSpace(SearchUnitypackageBoxInput.Text))
        {
            SearchUnitypackageBox.IsExpanded = false;
            QueueFilesExpander.IsExpanded = true;
        }
    }

    private async Task ExpandSearchBoxAndRefreshAsync(bool forceCheck)
    {
        var requirementsMet = await CheckSystemRequirementsAndUpdateUiAsync(forceCheck);
        if (requirementsMet)
        {
            SearchUnitypackageBox.IsExpanded = true;
            if (Resources["SearchResults"] is CollectionViewSource searchResults) searchResults.View.Refresh();
        }
    }

    private void SearchUnitypackageBoxResultsListView_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListView listView)
            return;
        var selectedItems = listView.SelectedItems;
        foreach (var item in selectedItems)
        {
            if (item is not SearchEverythingModel model)
                continue;
            var newUnitypackageModel = new UnitypackageFileInfo
            {
                FileName = model.FileName,
                FileHash = HashChecks.ComputeFileHash(new FileInfo(model.FilePath)),
                FileSize = model.FileSize,
                FileDate = model.ModifiedTime,
                FilePath = model.FilePath,
                FileExtension = Path.GetExtension(model.FileName),
                IsInQueue = true
            };
            ConfigHandler.Instance.Config.UnitypackageFiles.Add(newUnitypackageModel);
        }

        FilterQueue.FilterDuplicates();
        ConfigHandler.Instance.OverrideConfig();
        SyncFileCollections();
        SearchUnitypackageBox.IsExpanded = false;
    }

    private void SearchUnitypackageBoxInput_OnLostFocus(object sender, RoutedEventArgs e)
    {
        SearchUnitypackageBox.IsExpanded = false;
        QueueFilesExpander.IsExpanded = true;
    }

    private async void StartExtractionFromQueue()
    {
        BetterExtractionCard.Visibility = Visibility.Collapsed;
        CurrentlyExtractingCard.Visibility = Visibility.Visible;
        StartExtractionButton.IsEnabled = false;

        var queuedFiles = ConfigHandler.Instance.Config.UnitypackageFiles.Where(file => file.IsInQueue).ToList();
        queuedFiles.ForEach(file => file.IsExtracting = true); // Set all in queue to IsExtracting = true
        var totalFiles = queuedFiles.Count;
        if (totalFiles == 0)
        {
            ResetExtractionUi();
            return;
        }

        var overallStartTime = DateTime.Now;
        for (var processedFiles = 0; processedFiles < queuedFiles.Count; processedFiles++)
        {
            var file = queuedFiles[processedFiles];
            file.IsInQueue = false;
            ConfigHandler.Instance.OverrideConfig();
            SyncFileCollections();

            UpdateFileExtractionUi(file);

            _currentFileProgress.ExtractedCount = 0;
            _currentFileProgress.TotalEntryCount = 0;

            var unitypackageModel = new SearchEverythingModel { FileName = file.FileName, FilePath = file.FilePath };
            var fileExtractionProgress = new Progress<(int extracted, int total)>(progressData =>
            {
                _currentFileProgress.ExtractedCount = progressData.extracted;
                _currentFileProgress.TotalEntryCount = progressData.total;
                UpdateFileProgressUi();
            });

            var extractionTask = ExtractionHandler.ExtractUnitypackage(unitypackageModel, fileExtractionProgress);
            await MonitorProgressAsync(extractionTask, overallStartTime, totalFiles, processedFiles);

            if (await extractionTask)
                ConfigHandler.Instance.Config.UnitypackageFiles.Remove(file);
            else
                file.IsInQueue = true;

            ConfigHandler.Instance.OverrideConfig();
            SyncFileCollections();
        }

        ResetExtractionUi();
    }

    private void ResetExtractionUi()
    {
        CurrentlyExtractingCard.Visibility = Visibility.Collapsed;
        BetterExtractionCard.Visibility = Visibility.Visible;
        StartExtractionButton.IsEnabled = true;
    }

    private void UpdateFileExtractionUi(UnitypackageFileInfo file)
    {
        ExtractionTitleText.Text = $"Extracting File: {file.FileName}";
        ExtractionCaptionText.Text =
            $"({_currentFileProgress.ExtractedCount} of {_currentFileProgress.TotalEntryCount} files)";
    }

    private void UpdateFileProgressUi()
    {
        ExtractionCaptionText.Text =
            $"({_currentFileProgress.ExtractedCount} of {_currentFileProgress.TotalEntryCount} files)";
    }

    private async Task MonitorProgressAsync(Task extractionTask, DateTime overallStartTime, int totalFiles,
        int processedFiles)
    {
        while (!extractionTask.IsCompleted)
        {
            var elapsed = DateTime.Now - overallStartTime;

            Application.Current.Dispatcher.Invoke(() =>
            {
                ExtractionElapsedText.Text = elapsed.ToString(@"hh\:mm\:ss");

                var totalFilesToExtract = ConfigHandler.Instance.Config.TotalFilesToExtract;
                var currentExtractedCount = ConfigHandler.Instance.Config.CurrentExtractedCount;

                if (totalFilesToExtract > 0)
                {
                    var fileProgressPercent = (double)currentExtractedCount / totalFilesToExtract;
                    var overallPercent = (processedFiles + fileProgressPercent) / totalFiles * 100;

                    ExtractionProgressBar.Value = overallPercent;
                    ExtractionProgressText.Text = $"{overallPercent:0.00}%";
                    ExtractionCaptionText.Text = $"Extracted {currentExtractedCount} of {totalFilesToExtract} files";
                }
            });

            await Task.Delay(500); // Faster updates for smoother progress
        }
    }


    private void StartExtractionButton_OnClick(object sender, RoutedEventArgs e)
    {
        StartExtractionFromQueue();
        SyncFileCollections();
    }

    private void QueueFilesRemoveButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
            return;
        if (button.DataContext is not UnitypackageFileInfo file)
            return;
        ConfigHandler.Instance.Config.UnitypackageFiles.Remove(file);
        ConfigHandler.Instance.OverrideConfig();
        SyncFileCollections();
    }

    private void ViewExtractionButton_OnClick(object sender, RoutedEventArgs e)
    {
        Dashboard.Instance.NavView.Navigate(typeof(ExtractedContent));
    }
}