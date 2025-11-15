using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using EasyExtractCrossPlatform.Models;

namespace EasyExtractCrossPlatform.Services;

/// <summary>
///     Persists and restores window bounds while delegating storage to <see cref="AppSettings" />.
/// </summary>
public static class WindowPlacementService
{
    private static readonly object SyncRoot = new();
    private static Func<AppSettings?>? _settingsAccessor;
    private static Action<AppSettings>? _settingsPersister;

    public static void Configure(Func<AppSettings?> settingsAccessor, Action<AppSettings> settingsPersister)
    {
        _settingsAccessor = settingsAccessor ?? throw new ArgumentNullException(nameof(settingsAccessor));
        _settingsPersister = settingsPersister ?? throw new ArgumentNullException(nameof(settingsPersister));
    }

    public static IDisposable Attach(Window window, string windowKey, bool persistWindowState = false)
    {
        if (window is null)
            throw new ArgumentNullException(nameof(window));
        if (string.IsNullOrWhiteSpace(windowKey))
            throw new ArgumentException("Window key must be provided.", nameof(windowKey));

        return new WindowPlacementTracker(window, windowKey, persistWindowState);
    }

    internal static WindowPlacementState? GetPlacement(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return null;

        var settings = GetSettings();
        if (settings?.WindowPlacements is not { Count: > 0 } placements)
            return null;

        if (!placements.TryGetValue(key, out var stored) || stored is null)
            return null;

        return CreateState(stored);
    }

    internal static void SavePlacement(string key, WindowPlacementState placement)
    {
        if (string.IsNullOrWhiteSpace(key) || placement is null)
            return;

        var settings = GetSettings();
        if (settings is null)
            return;

        lock (SyncRoot)
        {
            var placements = EnsurePlacementsDictionary(settings);
            placements[key] = CreateSettings(placement);
            Persist(settings);
        }
    }

    private static Dictionary<string, WindowPlacementSettings> EnsurePlacementsDictionary(AppSettings settings)
    {
        var existing = settings.WindowPlacements;

        if (existing is not null && existing.Comparer == StringComparer.OrdinalIgnoreCase)
            return existing;

        var normalized = existing is null
            ? new Dictionary<string, WindowPlacementSettings>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, WindowPlacementSettings>(existing, StringComparer.OrdinalIgnoreCase);

        settings.WindowPlacements = normalized;
        return normalized;
    }

    private static AppSettings? GetSettings()
    {
        var accessor = _settingsAccessor;
        if (accessor is null)
            return null;

        try
        {
            return accessor();
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Failed to access window placement settings.", ex);
            return null;
        }
    }

    private static void Persist(AppSettings settings)
    {
        var persister = _settingsPersister;
        if (persister is null)
            return;

        try
        {
            persister(settings);
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Failed to persist window placement settings.", ex);
        }
    }

    private static WindowPlacementState CreateState(WindowPlacementSettings stored)
    {
        return new WindowPlacementState
        {
            Width = stored.Width,
            Height = stored.Height,
            PositionX = stored.PositionX,
            PositionY = stored.PositionY,
            WindowState = stored.WindowState
        };
    }

    private static WindowPlacementSettings CreateSettings(WindowPlacementState state)
    {
        return new WindowPlacementSettings
        {
            Width = state.Width,
            Height = state.Height,
            PositionX = state.PositionX,
            PositionY = state.PositionY,
            WindowState = state.WindowState
        };
    }

    private sealed class WindowPlacementTracker : IDisposable
    {
        private readonly bool _persistWindowState;
        private readonly Window _window;
        private readonly string _windowKey;
        private bool _hasRestoredPlacement;
        private bool _isDisposed;
        private PixelPoint? _lastNormalPosition;
        private Size? _lastNormalSize;

        public WindowPlacementTracker(Window window, string windowKey, bool persistWindowState)
        {
            _window = window;
            _windowKey = windowKey;
            _persistWindowState = persistWindowState;

            _window.Opened += OnWindowOpened;
            _window.Closing += OnWindowClosing;
            _window.PropertyChanged += OnWindowPropertyChanged;
            _window.PositionChanged += OnWindowPositionChanged;
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            _window.Opened -= OnWindowOpened;
            _window.Closing -= OnWindowClosing;
            _window.PropertyChanged -= OnWindowPropertyChanged;
            _window.PositionChanged -= OnWindowPositionChanged;
        }

        private void OnWindowOpened(object? sender, EventArgs e)
        {
            if (_hasRestoredPlacement)
                return;

            _hasRestoredPlacement = true;

            var placement = GetPlacement(_windowKey);
            if (placement is null)
                return;

            ApplyPlacement(placement);
        }

        private void ApplyPlacement(WindowPlacementState placement)
        {
            if (placement.Width.HasValue && placement.Width.Value > 0 && !double.IsNaN(placement.Width.Value))
                _window.Width = placement.Width.Value;

            if (placement.Height.HasValue && placement.Height.Value > 0 && !double.IsNaN(placement.Height.Value))
                _window.Height = placement.Height.Value;

            if (placement.Width.HasValue && placement.Height.HasValue &&
                placement.Width.Value > 0 && placement.Height.Value > 0)
                _lastNormalSize = new Size(placement.Width.Value, placement.Height.Value);

            if (placement.PositionX.HasValue && placement.PositionY.HasValue)
            {
                var requested = new PixelPoint(placement.PositionX.Value, placement.PositionY.Value);
                var adjusted = EnsureWindowIsVisible(requested, new Size(_window.Width, _window.Height));
                _window.Position = adjusted;
                _lastNormalPosition = adjusted;
            }

            if (_persistWindowState)
            {
                var restoredState = placement.WindowState == WindowState.Minimized
                    ? WindowState.Normal
                    : placement.WindowState;

                if (Enum.IsDefined(typeof(WindowState), restoredState))
                    _window.WindowState = restoredState;
            }

            CaptureCurrentBoundsIfNormal();
        }

        private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
        {
            SavePlacement();
        }

        private void SavePlacement()
        {
            var sizeToPersist = _window.WindowState == WindowState.Normal
                ? _window.Bounds.Size
                : _lastNormalSize ?? _window.Bounds.Size;
            var positionToPersist = _window.WindowState == WindowState.Normal
                ? _window.Position
                : _lastNormalPosition ?? _window.Position;

            var stateToPersist = _persistWindowState
                ? _window.WindowState == WindowState.Minimized
                    ? WindowState.Normal
                    : _window.WindowState
                : WindowState.Normal;

            var placement = new WindowPlacementState
            {
                Width = sizeToPersist.Width,
                Height = sizeToPersist.Height,
                PositionX = positionToPersist.X,
                PositionY = positionToPersist.Y,
                WindowState = stateToPersist
            };

            WindowPlacementService.SavePlacement(_windowKey, placement);
        }

        private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == Window.WindowStateProperty || e.Property == Window.BoundsProperty)
                CaptureCurrentBoundsIfNormal();
        }

        private void OnWindowPositionChanged(object? sender, PixelPointEventArgs e)
        {
            if (_window.WindowState == WindowState.Normal)
                _lastNormalPosition = e.Point;
        }

        private void CaptureCurrentBoundsIfNormal()
        {
            if (_window.WindowState != WindowState.Normal)
                return;

            _lastNormalSize = _window.Bounds.Size;
            _lastNormalPosition = _window.Position;
        }

        private PixelPoint EnsureWindowIsVisible(PixelPoint desiredPosition, Size windowSize)
        {
            if (_window.Screens is null)
                return desiredPosition;

            var pixelWidth = Math.Max(1, (int)Math.Round(windowSize.Width));
            var pixelHeight = Math.Max(1, (int)Math.Round(windowSize.Height));
            var targetScreen = _window.Screens.ScreenFromPoint(desiredPosition) ?? _window.Screens.Primary;

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
    }

    internal sealed class WindowPlacementState
    {
        public double? Width { get; set; }
        public double? Height { get; set; }
        public int? PositionX { get; set; }
        public int? PositionY { get; set; }
        public WindowState WindowState { get; set; } = WindowState.Normal;
    }
}