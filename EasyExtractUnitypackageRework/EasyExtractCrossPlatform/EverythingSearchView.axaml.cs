using Avalonia.Controls;
using Avalonia.Interactivity;
using EasyExtractCrossPlatform.ViewModels;

namespace EasyExtractCrossPlatform;

public partial class EverythingSearchView : UserControl
{
    private bool _initialized;

    public EverythingSearchView()
    {
        InitializeComponent();

        ViewModel = new EverythingSearchViewModel();
        DataContext = ViewModel;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public EverythingSearchViewModel ViewModel { get; }

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        ViewModel.AddToQueueRequested -= OnAddToQueueRequested;
        ViewModel.AddToQueueRequested += OnAddToQueueRequested;

        if (_initialized)
            return;

        _initialized = true;
        await ViewModel.InitializeAsync();
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        ViewModel.AddToQueueRequested -= OnAddToQueueRequested;
        ViewModel.Dispose();
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
    }

    private void OnAddToQueueRequested(object? sender, string packagePath)
    {
        if (string.IsNullOrWhiteSpace(packagePath))
            return;

        if (TopLevel.GetTopLevel(this) is MainWindow mainWindow)
            mainWindow.QueueUnityPackageFromSearch(packagePath);
    }
}