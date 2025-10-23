using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using EasyExtractCrossPlatform.ViewModels;

namespace EasyExtractCrossPlatform;

public partial class ReleaseNotesView : UserControl
{
    private readonly ReleaseNotesViewModel _viewModel;

    public ReleaseNotesView()
    {
        InitializeComponent();

        _viewModel = new ReleaseNotesViewModel();
        DataContext = _viewModel;

        AttachedToVisualTree += (_, _) => _ = LoadReleaseNotesAsync();
        DetachedFromVisualTree += (_, _) => _viewModel.Dispose();
    }

    public event EventHandler? CloseRequested;

    private async Task LoadReleaseNotesAsync()
    {
        await _viewModel.LoadAsync();
    }

    private async void RefreshButton_OnClick(object? sender, RoutedEventArgs e)
    {
        await _viewModel.LoadAsync();
    }

    private void CloseButton_OnClick(object? sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OpenReleaseLinkButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { CommandParameter: string url } || string.IsNullOrWhiteSpace(url))
            return;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            };

            Process.Start(psi);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to open release link: {ex}");
        }
    }
}