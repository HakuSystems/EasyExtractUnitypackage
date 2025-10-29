using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using EasyExtractCrossPlatform.Services;

namespace EasyExtractCrossPlatform;

public partial class FeedbackView : UserControl
{
    private readonly string? _appVersion;
    private readonly TextBox? _feedbackTextBox;
    private readonly Button? _sendButton;
    private readonly TextBlock? _statusTextBlock;
    private readonly TextBlock? _versionLabel;
    private bool _isSending;

    public FeedbackView(string? appVersion)
    {
        InitializeComponent();

        _appVersion = string.IsNullOrWhiteSpace(appVersion) ? null : appVersion.Trim();

        _feedbackTextBox = this.FindControl<TextBox>("FeedbackTextBox");
        _sendButton = this.FindControl<Button>("SendButton");
        _statusTextBlock = this.FindControl<TextBlock>("StatusTextBlock");
        _versionLabel = this.FindControl<TextBlock>("VersionLabel");

        if (_versionLabel is not null)
            _versionLabel.Text = _appVersion is null
                ? "Version: unknown"
                : $"Version: {_appVersion}";

        Loaded += OnLoaded;
    }

    public event EventHandler? CloseRequested;
    public event EventHandler? FeedbackSent;

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        await Task.Yield();
        _feedbackTextBox?.Focus();
    }

    private async void SendButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_isSending)
            return;

        var message = _feedbackTextBox?.Text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(message))
        {
            ShowStatus("Please enter a message before sending.", true);
            return;
        }

        _isSending = true;
        if (_sendButton is not null)
            _sendButton.IsEnabled = false;

        ShowStatus("Sending feedback...", isPending: true);

        try
        {
            await FeedbackService.SendFeedbackAsync(message, _appVersion).ConfigureAwait(true);
            _feedbackTextBox?.Clear();
            ShowStatus("Feedback sent. Thank you!");
            FeedbackSent?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            ShowStatus($"Failed to send feedback: {ex.Message}", true);
        }
        finally
        {
            _isSending = false;
            if (_sendButton is not null)
                _sendButton.IsEnabled = true;
        }
    }

    private void CancelButton_OnClick(object? sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ShowStatus(string message, bool isError = false, bool isPending = false)
    {
        if (_statusTextBlock is null)
            return;

        _statusTextBlock.Text = message;

        if (string.IsNullOrWhiteSpace(message))
        {
            _statusTextBlock.ClearValue(TextBlock.ForegroundProperty);
            return;
        }

        if (isError)
            _statusTextBlock.Foreground = Brushes.OrangeRed;
        else if (isPending)
            _statusTextBlock.Foreground = Brushes.Gray;
        else
            _statusTextBlock.ClearValue(TextBlock.ForegroundProperty);
    }
}