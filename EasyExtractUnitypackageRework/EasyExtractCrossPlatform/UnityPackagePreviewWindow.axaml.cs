using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using EasyExtractCrossPlatform.ViewModels;

namespace EasyExtractCrossPlatform;

public partial class UnityPackagePreviewWindow : Window
{
    public UnityPackagePreviewWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
        Closed += OnClosed;
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        if (DataContext is not UnityPackagePreviewViewModel viewModel)
            return;

        try
        {
            await viewModel.EnsureLoadedAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load package preview: {ex}");
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (DataContext is UnityPackagePreviewViewModel viewModel)
            viewModel.Dispose();

        Opened -= OnOpened;
        Closed -= OnClosed;
    }

    private void CloseButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}