namespace EasyExtractCrossPlatform;

public partial class DuplicateFinderWindow : Window
{
    private readonly IDisposable? _responsiveLayoutSubscription;
    private readonly IDisposable _windowPlacementTracker;

    public DuplicateFinderWindow()
    {
        InitializeComponent();
        LinuxUiHelper.ApplyWindowTweaks(this);
        _responsiveLayoutSubscription = ResponsiveWindowHelper.Enable(this);
        _windowPlacementTracker = WindowPlacementService.Attach(this, nameof(DuplicateFinderWindow));
    }

    public DuplicateFinderWindow(DuplicateFinderViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private DuplicateFinderViewModel? ViewModel => DataContext as DuplicateFinderViewModel;

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        ViewModel?.Dispose();
        _responsiveLayoutSubscription?.Dispose();
        _windowPlacementTracker.Dispose();
    }

    private async void ChooseFolderButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not { } viewModel)
            return;

        try
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Pick the folder that holds your .unitypackage files",
                AllowMultiple = false
            });

            if (folders.Count == 0)
                return;

            var path = folders[0].TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(path))
                return;

            await viewModel.ScanAsync(path);
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Failed to start a duplicate scan.", ex);
        }
    }

    private async void RescanButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not { LibraryPath: { Length: > 0 } path } viewModel)
            return;

        try
        {
            await viewModel.ScanAsync(path);
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Failed to rescan for duplicates.", ex);
        }
    }
}
