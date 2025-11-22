namespace EasyExtractCrossPlatform.Utilities;

public static class LinuxUiHelper
{
    public static void ApplyWindowTweaks(Window window)
    {
        if (!OperatingSystem.IsLinux())
            return;

        if (window is null)
            return;

        window.TransparencyLevelHint = new[] { WindowTransparencyLevel.None };

        if (window.Background is null &&
            window.TryFindResource("EasyWindowBackgroundBrush", out var backgroundResource) &&
            backgroundResource is IBrush brush)
            window.Background = brush;

        if (window.TransparencyBackgroundFallback is null)
            window.TransparencyBackgroundFallback = window.Background ?? Brushes.Transparent;

        if (window.TryFindResource("EasyStrokeBrush", out var borderResource) &&
            borderResource is IBrush borderBrush &&
            window.BorderBrush is null)
        {
            window.BorderBrush = borderBrush;
            window.BorderThickness = new Thickness(1);
        }
    }
}