using System.Windows.Data;
using EasyExtract.BetterExtraction;
using EasyExtract.Config;
using EasyExtract.Config.Models;
using EasyExtract.Services;
using EasyExtract.Utilities;

namespace EasyExtract.Controls;

public partial class BetterExtraction
{
    private const string QueueFilesKey = "QueueFiles";
    private const string ExtractingFilesKey = "ExtractingFiles";
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

    private void LocateUnitypackageButton_OnClick(object sender, RoutedEventArgs e)
    {
        UnitypackageLocator.LocateUnitypackageFiles();
        SyncFileCollections();
    }

    private void SyncFileCollections()
    {
        var queueFiles = Resources[QueueFilesKey] as CollectionViewSource ??
                         throw new InvalidOperationException($"Resource '{QueueFilesKey}' not found.");
        var extractingFiles = Resources[ExtractingFilesKey] as CollectionViewSource ??
                              throw new InvalidOperationException($"Resource '{ExtractingFilesKey}' not found.");
        UpdateClearQueueButtonVisibility();
        queueFiles.View.Refresh();
        extractingFiles.View.Refresh();
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
        ConfigHandler.Instance.Config.SearchEverythingResults.Clear();
        ConfigHandler.Instance.OverrideConfig();
        SetupFilter(QueueFilesKey, item => item is UnitypackageFileInfo file && file.IsInQueue);
        SetupFilter(ExtractingFilesKey, item => item is UnitypackageFileInfo file && !file.IsInQueue);

        var searchResults = Resources["SearchResults"] as CollectionViewSource ??
                            throw new InvalidOperationException("Resource 'SearchResults' not found.");
        searchResults.Filter += (s, args) =>
        {
            if (args.Item is SearchEverythingModel model)
            {
                var searchText = SearchUnitypackageBoxInput.Text;
                args.Accepted = string.IsNullOrWhiteSpace(searchText) ||
                                model.FileName.IndexOf(searchText, StringComparison.InvariantCultureIgnoreCase) >= 0;
            }
            else
            {
                args.Accepted = false;
            }
        };

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

        if (!forceCheck)
        {
            _hasCheckedSystemRequirements = true;
        }
        else
        {
            _recheckCount++;
        }

        var requirementsMet = await EverythingValidation.AreSystemRequirementsMetAsync();
        _systemRequirementsMet = requirementsMet;

        if (!requirementsMet)
        {
            SearchUnitypackageBox.Visibility = Visibility.Collapsed;
            SearchUnitypackageBoxExpanderError.Visibility = Visibility.Visible;
            var statusMessage = await EverythingValidation.GetSystemRequirementsStatusAsync();
            if (forceCheck)
                statusMessage = $"re-check (attempt #{_recheckCount}): " + statusMessage;
            SearchUnitypackageBoxFallback.Text = statusMessage;
            var logMessage = forceCheck
                ? $"System requirements still not met after fallback attempt #{_recheckCount}."
                : "System requirements not met for Search Everything";
            await BetterLogger.LogAsync(logMessage, Importance.Warning);
        }
        else
        {
            _recheckCount = 0;
            SearchUnitypackageBoxExpanderError.Visibility = Visibility.Collapsed;
            SearchUnitypackageBox.Visibility = Visibility.Visible;
            if (forceCheck)
                await BetterLogger.LogAsync("System requirements met after fallback attempt.", Importance.Info);
            Everything.Everything_SetSearchW("endwith:.unitypackage");
            Everything.Everything_SetRequestFlags(Everything.RequestFileName | Everything.RequestPath);
            Everything.Everything_QueryW(true);
            await Task.Run(PopulateSearchResultsAsync);
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
        await ExpandSearchBoxAndRefresh(false);
        if (string.IsNullOrWhiteSpace(SearchUnitypackageBoxInput.Text))
            return;
    }

    private async Task ExpandSearchBoxAndRefresh(bool forceCheck)
    {
        await CheckSystemRequirementsAndUpdateUiAsync(forceCheck);
        SearchUnitypackageBox.IsExpanded = true;
        var searchResults = Resources["SearchResults"] as CollectionViewSource;
        searchResults?.View.Refresh();
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

    private async void SearchUnitypackageBoxInput_OnGotFocus(object sender, RoutedEventArgs e)
    {
        await ExpandSearchBoxAndRefresh(true);
        QueueFilesExpander.IsExpanded = false;
        ExtractingFilesExpander.IsExpanded = false;
    }

    private void SearchUnitypackageBoxInput_OnLostFocus(object sender, RoutedEventArgs e)
    {
        SearchUnitypackageBox.IsExpanded = false;
        QueueFilesExpander.IsExpanded = true;
        ExtractingFilesExpander.IsExpanded = true;
    }
}