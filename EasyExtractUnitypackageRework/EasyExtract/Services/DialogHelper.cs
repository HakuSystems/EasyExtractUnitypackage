using Wpf.Ui;
using Wpf.Ui.Controls;

namespace EasyExtract.Services;

public class DialogHelper
{
    private static ContentPresenter? _dialogHost;
    private static IContentDialogService? _contentDialogService;

    public static async Task ShowErrorDialogAsync(Window? owner, string title, string message, string buttonText)
    {
        if (owner == null) return;

        if (_dialogHost == null)
        {
            Grid rootGrid;
            switch (owner.Content)
            {
                case null:
                    rootGrid = CreateRootGrid();
                    owner.Content = rootGrid;
                    break;
                case Grid existingGrid:
                    rootGrid = existingGrid;
                    break;
                default:
                {
                    var content = owner.Content;
                    rootGrid = CreateRootGrid();

                    owner.Content = rootGrid;

                    if (content is UIElement element) rootGrid.Children.Add(element);
                    break;
                }
            }

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

        var contentDialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = buttonText
        };

        await _contentDialogService.ShowAsync(contentDialog, default);
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
                    Height = new GridLength(1, GridUnitType.Auto)
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
                    Width = new GridLength(1, GridUnitType.Auto)
                }
            }
        };
    }
}