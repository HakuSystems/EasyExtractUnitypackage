namespace EasyExtractCrossPlatform;

public partial class MainWindow : Window
{
    private readonly record struct ExtractionItem(string Path, UnityPackageFile? QueueEntry);


    private enum SecurityBannerVisualState
    {
        Warning,
        Info
    }
}