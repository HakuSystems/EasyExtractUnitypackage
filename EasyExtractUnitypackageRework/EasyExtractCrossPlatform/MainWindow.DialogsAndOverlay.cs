namespace EasyExtractCrossPlatform;

public partial class MainWindow : Window
{
    private void SettingsButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_settingsWindow is { IsVisible: true })
        {
            _settingsWindow.Activate();
            return;
        }

        var window = new SettingsWindow();
        window.SettingsSaved += OnSettingsWindowSaved;
        window.Closed += OnSettingsWindowClosed;

        _settingsWindow = window;
        window.Show(this);
        UiSoundService.Instance.Play(UiSoundEffect.Subtle);
        QueueDiscordPresenceUpdate("Settings", "Tuning preferences");
    }

    private void CreditsButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_creditsWindow is { IsVisible: true })
        {
            _creditsWindow.Activate();
            return;
        }

        var window = new CreditsWindow();
        window.Closed += OnCreditsWindowClosed;

        _creditsWindow = window;
        window.Show(this);
        UiSoundService.Instance.Play(UiSoundEffect.Subtle);
        QueueDiscordPresenceUpdate("Credits", "Celebrating contributors");
    }

    private void HistoryButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_historyWindow is { IsVisible: true })
        {
            _historyWindow.Activate();
            return;
        }

        try
        {
            var viewModel = new HistoryViewModel(_settings.History);
            var window = new HistoryWindow(viewModel);

            window.Closed += (_, _) =>
            {
                if (ReferenceEquals(_historyWindow, window))
                    _historyWindow = null;
            };

            _historyWindow = window;
            window.Show(this);
            UiSoundService.Instance.Play(UiSoundEffect.Subtle);
            QueueDiscordPresenceUpdate("History", "Reviewing extraction trends");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to open history window: {ex}");
            ShowDropStatusMessage(
                "Unable to open history",
                ex.Message,
                TimeSpan.FromSeconds(4),
                UiSoundEffect.Negative);
        }
    }

    private void FeedbackButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_feedbackWindow is { IsVisible: true })
        {
            _feedbackWindow.Activate();
            return;
        }

        var window = new FeedbackWindow(GetCurrentVersionForComparison());
        window.FeedbackSent += OnFeedbackWindowSent;
        window.Closed += OnFeedbackWindowClosed;

        _feedbackWindow = window;
        window.Show(this);
        UiSoundService.Instance.Play(UiSoundEffect.Subtle);
        QueueDiscordPresenceUpdate("Feedback", "Drafting feedback");
    }

    private void OnSettingsWindowSaved(object? sender, AppSettings settings)
    {
        if (sender is not SettingsWindow)
            return;

        _settings = settings;
        ApplySettings(_settings);
        SetVersionText();
    }

    private void OnSettingsWindowClosed(object? sender, EventArgs e)
    {
        if (sender is not SettingsWindow window)
            return;

        window.SettingsSaved -= OnSettingsWindowSaved;
        window.Closed -= OnSettingsWindowClosed;

        if (ReferenceEquals(_settingsWindow, window))
            _settingsWindow = null;

        RestorePresenceAfterOverlay();
    }

    private void OnCreditsWindowClosed(object? sender, EventArgs e)
    {
        if (sender is not CreditsWindow window)
            return;

        window.Closed -= OnCreditsWindowClosed;

        if (ReferenceEquals(_creditsWindow, window))
            _creditsWindow = null;

        RestorePresenceAfterOverlay();
    }

    private void OnFeedbackWindowSent(object? sender, EventArgs e)
    {
        ShowDropStatusMessage(
            "Feedback sent",
            "Thanks for helping us improve EasyExtract.",
            TimeSpan.FromSeconds(4),
            UiSoundEffect.Positive);
    }

    private void OnFeedbackWindowClosed(object? sender, EventArgs e)
    {
        if (sender is not FeedbackWindow window)
            return;

        window.FeedbackSent -= OnFeedbackWindowSent;
        window.Closed -= OnFeedbackWindowClosed;

        if (ReferenceEquals(_feedbackWindow, window))
            _feedbackWindow = null;

        RestorePresenceAfterOverlay();
    }

    private async Task ShowOverlayAsync(Control view)
    {
        if (_overlayHost is null || _overlayContent is null)
            return;

        if (_activeOverlayContent is not null)
            await CloseOverlayAsync();

        _activeOverlayContent = view;
        _overlayContent.Content = view;
        await RunOverlayAnimationAsync(true);
        UiSoundService.Instance.Play(UiSoundEffect.Subtle);
        QueueDiscordPresenceUpdate(
            ResolveOverlayPresenceState(view),
            ResolveOverlayPresenceDetail(view));
    }

    private async Task CloseOverlayAsync(Control? requestingView = null)
    {
        if (_overlayHost is null || _overlayContent is null)
            return;

        if (_activeOverlayContent is null)
            return;

        if (requestingView is not null && !ReferenceEquals(_activeOverlayContent, requestingView))
            return;

        await RunOverlayAnimationAsync(false);

        DetachOverlayHandlers(_activeOverlayContent);

        _overlayContent.Content = null;
        _activeOverlayContent = null;
        RestorePresenceAfterOverlay();
    }

    private void DetachOverlayHandlers(Control control)
    {
    }

    private async Task RunOverlayAnimationAsync(bool showing)
    {
        if (_overlayHost is null)
            return;

        _overlayAnimationCts?.Cancel();
        _overlayAnimationCts?.Dispose();

        var cts = new CancellationTokenSource();
        _overlayAnimationCts = cts;
        var token = cts.Token;

        var scaleTransform = _overlayCardScaleTransform;
        if (scaleTransform is null && _overlayCard is not null)
        {
            scaleTransform = new ScaleTransform(1, 1);
            _overlayCard.RenderTransform = scaleTransform;
            _overlayCardScaleTransform = scaleTransform;
        }

        const double collapsedScale = 0.94;
        const double hideScale = 0.96;
        var showDuration = TimeSpan.FromMilliseconds(240);
        var hideDuration = TimeSpan.FromMilliseconds(180);

        if (!showing && !_overlayHost.IsVisible)
        {
            _overlayHost.IsHitTestVisible = false;
            _overlayHost.Opacity = 0;
            if (scaleTransform is not null)
            {
                scaleTransform.ScaleX = collapsedScale;
                scaleTransform.ScaleY = collapsedScale;
            }

            cts.Dispose();
            if (ReferenceEquals(_overlayAnimationCts, cts))
                _overlayAnimationCts = null;
            return;
        }

        try
        {
            if (showing)
            {
                _overlayHost.IsVisible = true;
                _overlayHost.IsHitTestVisible = true;
                _overlayHost.Opacity = 0;
                if (scaleTransform is not null)
                {
                    scaleTransform.ScaleX = collapsedScale;
                    scaleTransform.ScaleY = collapsedScale;
                }

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    _overlayHost.Opacity = 1;
                    if (scaleTransform is not null)
                    {
                        scaleTransform.ScaleX = 1;
                        scaleTransform.ScaleY = 1;
                    }
                }, DispatcherPriority.Render);

                await Task.Delay(showDuration, token);
                if (token.IsCancellationRequested)
                    return;
            }
            else
            {
                _overlayHost.IsHitTestVisible = false;

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    _overlayHost.Opacity = 0;
                    if (scaleTransform is not null)
                    {
                        scaleTransform.ScaleX = hideScale;
                        scaleTransform.ScaleY = hideScale;
                    }
                }, DispatcherPriority.Render);

                await Task.Delay(hideDuration, token);
                if (token.IsCancellationRequested)
                    return;

                _overlayHost.IsVisible = false;
                _overlayHost.Opacity = 0;
                if (scaleTransform is not null)
                {
                    scaleTransform.ScaleX = collapsedScale;
                    scaleTransform.ScaleY = collapsedScale;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Ignore cancellations triggered by a new animation request.
        }
        finally
        {
            if (ReferenceEquals(_overlayAnimationCts, cts))
            {
                _overlayAnimationCts.Dispose();
                _overlayAnimationCts = null;
            }
        }
    }

}

