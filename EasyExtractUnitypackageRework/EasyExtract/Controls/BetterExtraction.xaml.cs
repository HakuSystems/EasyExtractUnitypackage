﻿using System.Windows.Data;
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
        queueFiles.View.Refresh();
        extractingFiles.View.Refresh();
    }

    private async void BetterExtraction_OnLoaded(object sender, RoutedEventArgs e)
    {
        ConfigHandler.Instance.Config.SearchEverythingResults.Clear();
        await CheckSystemRequirementsAndUpdateUiAsync(true);
        ConfigHandler.Instance.OverrideConfig();
        var queueFiles = Resources[QueueFilesKey] as CollectionViewSource ??
                         throw new InvalidOperationException($"Resource '{QueueFilesKey}' not found.");
        queueFiles.Filter += (s, args) =>
        {
            if (args.Item is UnitypackageFileInfo file)
                args.Accepted = file.IsInQueue;
            else
                args.Accepted = false;
        };
        var extractingFiles = Resources[ExtractingFilesKey] as CollectionViewSource ??
                              throw new InvalidOperationException($"Resource '{ExtractingFilesKey}' not found.");
        extractingFiles.Filter += (s, args) =>
        {
            if (args.Item is UnitypackageFileInfo file)
                args.Accepted = !file.IsInQueue;
            else
                args.Accepted = false;
        };
        var searchResults = Resources["SearchResults"] as CollectionViewSource ??
                            throw new InvalidOperationException("Resource 'SearchResults' not found.");
        searchResults.Filter += (s, args) =>
        {
            if (args.Item is SearchEverythingModel model)
            {
                var searchText = SearchUnitypackageBoxInput.Text;
                if (string.IsNullOrWhiteSpace(searchText))
                    args.Accepted = true;
                else
                    args.Accepted = model.FileName.IndexOf(searchText, StringComparison.InvariantCultureIgnoreCase) >=
                                    0;
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
            ConfigHandler.Instance.OverrideConfig();
        }
    }

    private async Task<bool> CheckSystemRequirementsAndUpdateUiAsync(bool forceCheck)
    {
        if (!forceCheck && _hasCheckedSystemRequirements)
            return true;
        if (!forceCheck)
            _hasCheckedSystemRequirements = true;
        else
            _recheckCount++;
        var requirementsMet = await EverythingValidation.AreSystemRequirementsMetAsync();
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
        await CheckSystemRequirementsAndUpdateUiAsync(true);
    }

    private void ClearQueueButton_OnClick(object sender, RoutedEventArgs e)
    {
        ConfigHandler.Instance.Config.UnitypackageFiles.Clear();
        ConfigHandler.Instance.OverrideConfig();
        SyncFileCollections();
    }

    private async void SearchUnitypackageBoxInput_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        await CheckSystemRequirementsAndUpdateUiAsync(false);
        SearchUnitypackageBox.IsExpanded = true;
        var searchResults = Resources["SearchResults"] as CollectionViewSource;
        if (searchResults != null)
            searchResults.View.Refresh();
        if (string.IsNullOrWhiteSpace(SearchUnitypackageBoxInput.Text))
            return;
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
}