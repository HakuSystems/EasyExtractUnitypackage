using System.Windows.Data;
using EasyExtract.BetterExtraction;
using EasyExtract.Config;
using Wpf.Ui.Controls;

namespace EasyExtract.Controls;

public partial class BetterExtraction
{
    // Constants for resource keys
    private const string QueueFilesKey = "QueueFiles";
    private const string ExtractingFilesKey = "ExtractingFiles";

    public BetterExtraction()
    {
        InitializeComponent();
        DataContext = ConfigHandler.Instance.Config;
    }

    private LocateUnitypackage UnitypackageLocator { get; } = new();

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
                args.Accepted = file.IsInQueue; // Only show items with IsInQueue == true
            else
                args.Accepted = false;
        };

        var extractingFiles = Resources[ExtractingFilesKey] as CollectionViewSource
                              ?? throw new InvalidOperationException($"Resource '{ExtractingFilesKey}' not found.");
        extractingFiles.Filter += (s, args) =>
        {
            if (args.Item is UnitypackageFileInfo file)
                args.Accepted = !file.IsInQueue; // Only show items with IsInQueue == false
            else
                args.Accepted = false;
        };

        SyncFileCollections();
    }

    private void SearchUnitypackageBox_OnTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        //todo ignore
    }
}