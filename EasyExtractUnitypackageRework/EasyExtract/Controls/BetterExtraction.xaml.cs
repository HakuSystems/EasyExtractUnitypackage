using System.Windows.Data;
using EasyExtract.BetterExtraction;
using EasyExtract.Config;

namespace EasyExtract.Controls;

public partial class BetterExtraction
{
    public BetterExtraction()
    {
        InitializeComponent();
        DataContext = ConfigHandler.Instance.Config;
    }

    private LocateUnitypackage LocateUnitypackage { get; } = new();

    private void LocateUnitypackageButton_OnClick(object sender, RoutedEventArgs e)
    {
        LocateUnitypackage.LocateUnitypackageFiles();
        SyncFileCollections();
    }

    private void SyncFileCollections()
    {
        // Refresh views
        var queueFiles = Resources["QueueFiles"] as CollectionViewSource ?? throw new InvalidOperationException();
        var extractingFiles =
            Resources["ExtractingFiles"] as CollectionViewSource ?? throw new InvalidOperationException();
        queueFiles.View.Refresh();
        extractingFiles.View.Refresh();
    }

    private void BetterExtraction_OnLoaded(object sender, RoutedEventArgs e)
    {
        var queueFiles = Resources["QueueFiles"] as CollectionViewSource ?? throw new InvalidOperationException();
        queueFiles.Filter += (_, args) =>
        {
            if (args.Item is UnitypackageFileInfo file)
                args.Accepted = file.IsInQueue; // Only show items with IsInQueue == true
            else
                args.Accepted = false;
        };

        var extractingFiles =
            Resources["ExtractingFiles"] as CollectionViewSource ?? throw new InvalidOperationException();
        extractingFiles.Filter += (_, args) =>
        {
            if (args.Item is UnitypackageFileInfo file)
                args.Accepted = !file.IsInQueue; // Only show items with IsInQueue == false
            else
                args.Accepted = false;
        };

        SyncFileCollections();
    }
}