namespace EasyExtractCrossPlatform;

public partial class MainWindow : Window
{
    private readonly record struct ExtractionItem(
        string Path,
        UnityPackageFile? QueueEntry,
        IReadOnlyCollection<string>? IncludeAssetKeys = null);


    private enum SecurityBannerVisualState
    {
        Warning,
        Info
    }
}