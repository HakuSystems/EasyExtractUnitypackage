namespace EasyExtractCrossPlatform;

public partial class MainWindow : Window
{
    private void LoadSettings()
    {
        _settings = AppSettingsService.Load();
        WindowPlacementService.Configure(() => _settings, AppSettingsService.Save);
        ApplyWindowPlacement(_settings);
        ApplySettings(_settings);
    }

    private void ApplyWindowPlacement(AppSettings settings)
    {
        var sharedPlacement = WindowPlacementService.GetPlacement(nameof(MainWindow));
        if (sharedPlacement is not null)
        {
            ApplyWindowPlacementState(sharedPlacement);
            return;
        }

        var savedWidth = settings.WindowWidth;
        var savedHeight = settings.WindowHeight;

        if (savedWidth.HasValue && savedWidth.Value > 0 && !double.IsNaN(savedWidth.Value))
            Width = savedWidth.Value;

        if (savedHeight.HasValue && savedHeight.Value > 0 && !double.IsNaN(savedHeight.Value))
            Height = savedHeight.Value;

        if (savedWidth.HasValue && savedHeight.HasValue &&
            savedWidth.Value > 0 && savedHeight.Value > 0 &&
            !double.IsNaN(savedWidth.Value) && !double.IsNaN(savedHeight.Value))
            _lastNormalSize = new Size(savedWidth.Value, savedHeight.Value);

        if (settings.WindowPositionX.HasValue && settings.WindowPositionY.HasValue)
        {
            var requestedPosition = new PixelPoint(settings.WindowPositionX.Value, settings.WindowPositionY.Value);
            var adjustedPosition = EnsureWindowIsVisible(requestedPosition, new Size(Width, Height));
            Position = adjustedPosition;
            _lastNormalPosition = adjustedPosition;
        }

        var restoredState = settings.WindowState == WindowState.Minimized
            ? WindowState.Normal
            : settings.WindowState;

        WindowState = Enum.IsDefined(typeof(WindowState), restoredState)
            ? restoredState
            : WindowState.Normal;

        CaptureCurrentBoundsIfNormal();
    }

    private void ApplyWindowPlacementState(WindowPlacementService.WindowPlacementState placement)
    {
        if (placement.Width.HasValue && placement.Width.Value > 0 && !double.IsNaN(placement.Width.Value))
        {
            Width = placement.Width.Value;
            _settings.WindowWidth = placement.Width.Value;
        }

        if (placement.Height.HasValue && placement.Height.Value > 0 && !double.IsNaN(placement.Height.Value))
        {
            Height = placement.Height.Value;
            _settings.WindowHeight = placement.Height.Value;
        }

        if (placement.Width.HasValue && placement.Height.HasValue &&
            placement.Width.Value > 0 && placement.Height.Value > 0)
            _lastNormalSize = new Size(placement.Width.Value, placement.Height.Value);

        if (placement.PositionX.HasValue && placement.PositionY.HasValue)
        {
            var requestedPosition = new PixelPoint(placement.PositionX.Value, placement.PositionY.Value);
            var adjustedPosition = EnsureWindowIsVisible(requestedPosition, new Size(Width, Height));
            Position = adjustedPosition;
            _lastNormalPosition = adjustedPosition;
            _settings.WindowPositionX = adjustedPosition.X;
            _settings.WindowPositionY = adjustedPosition.Y;
        }

        var restoredState = placement.WindowState == WindowState.Minimized
            ? WindowState.Normal
            : placement.WindowState;

        if (Enum.IsDefined(typeof(WindowState), restoredState))
        {
            WindowState = restoredState;
            _settings.WindowState = restoredState;
        }

        CaptureCurrentBoundsIfNormal();
    }

    private PixelPoint EnsureWindowIsVisible(PixelPoint desiredPosition, Size windowSize)
    {
        if (Screens is null)
            return desiredPosition;

        var pixelWidth = Math.Max(1, (int)Math.Round(windowSize.Width));
        var pixelHeight = Math.Max(1, (int)Math.Round(windowSize.Height));
        var targetScreen = Screens.ScreenFromPoint(desiredPosition) ?? Screens.Primary;

        if (targetScreen is null)
            return desiredPosition;

        var workingArea = targetScreen.WorkingArea;
        var left = workingArea.X;
        var top = workingArea.Y;
        var maxX = left + workingArea.Width - pixelWidth;
        var maxY = top + workingArea.Height - pixelHeight;
        var clampedX = Math.Clamp(desiredPosition.X, left, Math.Max(left, maxX));
        var clampedY = Math.Clamp(desiredPosition.Y, top, Math.Max(top, maxY));

        return new PixelPoint(clampedX, clampedY);
    }

    private void CaptureCurrentBoundsIfNormal()
    {
        if (WindowState != WindowState.Normal)
            return;

        _lastNormalSize = Bounds.Size;
        _lastNormalPosition = Position;
    }

    private void OnMainWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == WindowStateProperty)
        {
            CaptureCurrentBoundsIfNormal();
            return;
        }

        if (e.Property == BoundsProperty)
            CaptureCurrentBoundsIfNormal();
    }

    private void OnMainWindowPositionChanged(object? sender, PixelPointEventArgs e)
    {
        if (WindowState == WindowState.Normal)
            _lastNormalPosition = e.Point;
    }

    private void OnMainWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        SaveWindowPlacement();
        if (SearchViewModel is not null)
            SearchViewModel.PropertyChanged -= OnSearchViewModelPropertyChanged;

        _extractionElapsedTimer.Stop();
        _extractionElapsedTimer.Tick -= OnExtractionElapsedTick;
        AppSettingsService.Save(_settings);
    }

    private void SaveWindowPlacement()
    {
        var sizeToPersist = WindowState == WindowState.Normal
            ? Bounds.Size
            : _lastNormalSize ?? Bounds.Size;
        var positionToPersist = WindowState == WindowState.Normal
            ? Position
            : _lastNormalPosition ?? Position;

        _settings.WindowWidth = sizeToPersist.Width;
        _settings.WindowHeight = sizeToPersist.Height;
        _settings.WindowPositionX = positionToPersist.X;
        _settings.WindowPositionY = positionToPersist.Y;
        _settings.WindowState = WindowState == WindowState.Minimized ? WindowState.Normal : WindowState;

        var placementState = new WindowPlacementService.WindowPlacementState
        {
            Width = _settings.WindowWidth,
            Height = _settings.WindowHeight,
            PositionX = _settings.WindowPositionX,
            PositionY = _settings.WindowPositionY,
            WindowState = _settings.WindowState
        };

        WindowPlacementService.SavePlacement(nameof(MainWindow), placementState);
    }

    private void ApplySettings(AppSettings settings)
    {
        UiSoundService.Instance.UpdateSettings(settings);
        ReloadQueueFromSettings();
        ApplyTheme(settings.ApplicationTheme);
        _ = ApplyCustomBackgroundAsync(settings);
        ApplyUwUMode(settings);
        UpdateExtractionOverviewDisplay();
        QueueDiscordPresenceUpdate("Dashboard", settingsOverride: settings);
        ContextMenuIntegrationService.UpdateContextMenuIntegration(settings.ContextMenuToggle);
    }

    private void ApplyUwUMode(AppSettings settings)
    {
        var isActive = settings.UwUModeActive;

        if (_uwuModeBanner is not null)
            _uwuModeBanner.IsVisible = isActive;

        _defaultDropPrimaryText = isActive && !string.IsNullOrWhiteSpace(_uwuDropPrimaryText)
            ? _uwuDropPrimaryText
            : _standardDropPrimaryText;

        _defaultDropSecondaryText = isActive && !string.IsNullOrWhiteSpace(_uwuDropSecondaryText)
            ? _uwuDropSecondaryText
            : _standardDropSecondaryText;

        RefreshDropZoneBaseTextIfIdle();
    }
}