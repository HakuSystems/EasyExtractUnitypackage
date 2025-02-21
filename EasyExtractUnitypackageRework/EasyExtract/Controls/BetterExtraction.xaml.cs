using System.Windows.Data;
using EasyExtract.BetterExtraction;
using EasyExtract.Config;
using EasyExtract.Config.Models;
using EasyExtract.Services;
using EasyExtract.Utilities;
using Wpf.Ui.Controls;

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

    private void LocateUnitypackageButton_OnClick(object sender, RoutedEventArgs e)
    {
        UnitypackageLocator.LocateUnitypackageFiles();
        SyncFileCollections();
    }

    private void SyncFileCollections()
    {
        var queueFiles = Resources[QueueFilesKey] as CollectionViewSource
                         ?? throw new InvalidOperationException($"Resource '{QueueFilesKey}' not found.");
        var extractingFiles = Resources[ExtractingFilesKey] as CollectionViewSource
                              ?? throw new InvalidOperationException($"Resource '{ExtractingFilesKey}' not found.");
        queueFiles.View.Refresh();
        extractingFiles.View.Refresh();
    }

    private void BetterExtraction_OnLoaded(object sender, RoutedEventArgs e)
    {
        var queueFiles = Resources[QueueFilesKey] as CollectionViewSource
                         ?? throw new InvalidOperationException($"Resource '{QueueFilesKey}' not found.");
        queueFiles.Filter += (s, args) =>
        {
            if (args.Item is UnitypackageFileInfo file)
                args.Accepted = file.IsInQueue;
            else
                args.Accepted = false;
        };

        var extractingFiles = Resources[ExtractingFilesKey] as CollectionViewSource
                              ?? throw new InvalidOperationException($"Resource '{ExtractingFilesKey}' not found.");
        extractingFiles.Filter += (s, args) =>
        {
            if (args.Item is UnitypackageFileInfo file)
                args.Accepted = !file.IsInQueue;
            else
                args.Accepted = false;
        };

        SyncFileCollections();
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
        }

        return requirementsMet;
    }

    private async void SearchUnitypackageBox_OnTextChanged(AutoSuggestBox sender,
        AutoSuggestBoxTextChangedEventArgs args)
    {
        await CheckSystemRequirementsAndUpdateUiAsync(false);
    }

    private async void SearchUnitypackageBoxFallbackButton_OnClick(object sender, RoutedEventArgs e)
    {
        await CheckSystemRequirementsAndUpdateUiAsync(true);
    }
}