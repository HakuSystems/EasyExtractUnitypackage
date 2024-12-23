using Wpf.Ui;
using Wpf.Ui.Controls;
using Application = System.Windows.Application;

namespace EasyExtract.Services;

public static class DialogHelper
{
    private static ContentPresenter? _dialogHost;
    private static IContentDialogService? _contentDialogService;

    public static async Task ShowInfoDialogAsync(Window? owner, string title, string message, string buttonText = "OK")
    {
        owner ??= Application.Current.MainWindow;

        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            if (_dialogHost == null)
            {
                var rootGrid = EnsureRootGrid(owner);

                _dialogHost = new ContentPresenter
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch
                };

                _dialogHost.SetValue(Grid.RowProperty, 1);
                _dialogHost.SetValue(Grid.ColumnProperty, 0);
                _dialogHost.SetValue(Grid.ColumnSpanProperty, int.MaxValue);
                _dialogHost.SetValue(Grid.RowSpanProperty, int.MaxValue);
                _dialogHost.SetValue(Panel.ZIndexProperty, int.MaxValue);

                rootGrid.Children.Add(_dialogHost);
            }

            if (_contentDialogService == null)
            {
                _contentDialogService = new ContentDialogService();
                _contentDialogService.SetDialogHost(_dialogHost);
            }
        });

        var contentDialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = buttonText
        };

        await _contentDialogService!.ShowAsync(contentDialog, default).ConfigureAwait(false);
    }


    public static async Task ShowErrorDialogAsync(Window? owner, string title, string message, string buttonText)
    {
        owner ??= Application.Current.MainWindow;

        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            if (_dialogHost == null)
            {
                var rootGrid = EnsureRootGrid(owner);

                _dialogHost = new ContentPresenter
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch
                };

                _dialogHost.SetValue(Grid.RowProperty, 1);
                _dialogHost.SetValue(Grid.ColumnProperty, 0);
                _dialogHost.SetValue(Grid.ColumnSpanProperty, int.MaxValue);
                _dialogHost.SetValue(Grid.RowSpanProperty, int.MaxValue);
                _dialogHost.SetValue(Panel.ZIndexProperty, int.MaxValue);

                rootGrid.Children.Add(_dialogHost);
            }

            if (_contentDialogService == null)
            {
                _contentDialogService = new ContentDialogService();
                _contentDialogService.SetDialogHost(_dialogHost);
            }
        });

        var contentDialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = buttonText
        };

        await _contentDialogService!.ShowAsync(contentDialog, default).ConfigureAwait(false);
    }

    private static Grid EnsureRootGrid(Window owner)
    {
        if (owner.Content is Grid existingGrid)
            return existingGrid;

        var content = owner.Content;
        var rootGrid = CreateRootGrid();
        owner.Content = rootGrid;

        if (content is UIElement element)
            rootGrid.Children.Add(element);

        return rootGrid;
    }

    private static Grid CreateRootGrid()
    {
        return new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            RowDefinitions =
            {
                new RowDefinition
                {
                    Height = new GridLength(1, GridUnitType.Star)
                },
                new RowDefinition
                {
                    Height = GridLength.Auto
                }
            },
            ColumnDefinitions =
            {
                new ColumnDefinition
                {
                    Width = new GridLength(1, GridUnitType.Star)
                },
                new ColumnDefinition
                {
                    Width = GridLength.Auto
                }
            }
        };
    }
}