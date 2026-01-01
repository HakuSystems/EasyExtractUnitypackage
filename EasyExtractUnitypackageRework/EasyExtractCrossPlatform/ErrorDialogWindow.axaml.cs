namespace EasyExtractCrossPlatform;

public partial class ErrorDialogWindow : Window
{
    // Parameterless constructor required for Avalonia XAML loader and design tools.
    public ErrorDialogWindow() : this("EasyExtract ran into a problem", "An unexpected error occurred.")
    {
    }

    public ErrorDialogWindow(string title, string message, string? detail = null)
    {
        InitializeComponent();

        TitleTextBlock.Text = string.IsNullOrWhiteSpace(title) ? "Something broke" : title;
        MessageTextBlock.Text = string.IsNullOrWhiteSpace(message)
            ? "An unexpected error occurred."
            : message.Trim();

        if (string.IsNullOrWhiteSpace(detail))
        {
            DetailsCard.IsVisible = false;
        }
        else
        {
            DetailsCard.IsVisible = true;
            DetailsTextBox.Text = detail.Trim();
        }
    }

    private void CloseButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OpenLogsButton_OnClick(object? sender, RoutedEventArgs e)
    {
        LoggingService.TryOpenLogFolder();
    }

    private async void CopyButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = GetTopLevel(this);
        var textToCopy = string.IsNullOrWhiteSpace(DetailsTextBox.Text)
            ? MessageTextBlock.Text
            : DetailsTextBox.Text;

        if (topLevel?.Clipboard is { } clipboard && !string.IsNullOrWhiteSpace(textToCopy))
            await clipboard.SetTextAsync(textToCopy);
    }
}