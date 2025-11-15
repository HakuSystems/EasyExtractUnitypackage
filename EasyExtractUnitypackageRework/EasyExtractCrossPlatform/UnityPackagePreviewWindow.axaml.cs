using System;
using System.Collections.Specialized;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using EasyExtractCrossPlatform.Services;
using EasyExtractCrossPlatform.Utilities;
using EasyExtractCrossPlatform.ViewModels;

namespace EasyExtractCrossPlatform;

public partial class UnityPackagePreviewWindow : Window
{
    private readonly Border? _assetTreeCard;
    private readonly Grid? _detailsGrid;
    private readonly Border? _inspectorCard;
    private readonly IDisposable? _responsiveLayoutSubscription;
    private readonly IDisposable _windowPlacementTracker;

    public UnityPackagePreviewWindow()
    {
        InitializeComponent();
        LinuxUiHelper.ApplyWindowTweaks(this);
        _responsiveLayoutSubscription = ResponsiveWindowHelper.Enable(this);
        _windowPlacementTracker = WindowPlacementService.Attach(this, nameof(UnityPackagePreviewWindow));
        _detailsGrid = this.FindControl<Grid>("DetailsGrid");
        _assetTreeCard = this.FindControl<Border>("AssetTreeCard");
        _inspectorCard = this.FindControl<Border>("InspectorCard");
        Classes.CollectionChanged += OnClassesChanged;
        ApplyResponsiveLayout();
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
        Classes.CollectionChanged -= OnClassesChanged;
        _responsiveLayoutSubscription?.Dispose();
        _windowPlacementTracker.Dispose();
    }

    private void OnClassesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ApplyResponsiveLayout();
    }

    private void ApplyResponsiveLayout()
    {
        if (_detailsGrid is null || _assetTreeCard is null || _inspectorCard is null)
            return;

        if (Classes.Contains("compact"))
        {
            _detailsGrid.ColumnDefinitions = new ColumnDefinitions("*");
            _detailsGrid.RowDefinitions = new RowDefinitions("Auto,Auto");
            Grid.SetColumn(_assetTreeCard, 0);
            Grid.SetRow(_assetTreeCard, 0);
            Grid.SetColumn(_inspectorCard, 0);
            Grid.SetRow(_inspectorCard, 1);
            _inspectorCard.Margin = new Thickness(0);
        }
        else
        {
            _detailsGrid.ColumnDefinitions = new ColumnDefinitions("2*,*");
            _detailsGrid.RowDefinitions = new RowDefinitions("Auto");
            Grid.SetColumn(_assetTreeCard, 0);
            Grid.SetRow(_assetTreeCard, 0);
            Grid.SetColumn(_inspectorCard, 1);
            Grid.SetRow(_inspectorCard, 0);
            _inspectorCard.Margin = new Thickness(24, 0, 0, 0);
        }
    }
}