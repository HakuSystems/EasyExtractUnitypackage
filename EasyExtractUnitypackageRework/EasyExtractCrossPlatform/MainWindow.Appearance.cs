namespace EasyExtractCrossPlatform;

public partial class MainWindow : Window
{
    private async Task ApplyCustomBackgroundAsync(AppSettings settings)
    {
        var backgroundSettings = settings.CustomBackgroundImage;
        if (backgroundSettings is null)
        {
            await Dispatcher.UIThread.InvokeAsync(() => SetBackgroundBrush(_defaultBackgroundBrush, null));
            return;
        }

        if (!backgroundSettings.IsEnabled)
        {
            await Dispatcher.UIThread.InvokeAsync(() => SetBackgroundBrush(_defaultBackgroundBrush, null));
            return;
        }

        var backgroundPath = backgroundSettings.BackgroundPath;
        if (string.IsNullOrWhiteSpace(backgroundPath))
        {
            await Dispatcher.UIThread.InvokeAsync(() => SetBackgroundBrush(_defaultBackgroundBrush, null));
            return;
        }

        var opacity = Math.Clamp(backgroundSettings.BackgroundOpacity, 0.0, 1.0);
        var bitmap = await LoadBackgroundBitmapAsync(backgroundPath);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (bitmap is null)
            {
                SetBackgroundBrush(_defaultBackgroundBrush, null);
                return;
            }

            var imageBrush = new ImageBrush(bitmap)
            {
                Stretch = Stretch.UniformToFill,
                AlignmentX = AlignmentX.Center,
                AlignmentY = AlignmentY.Center,
                Opacity = opacity
            };

            SetBackgroundBrush(imageBrush, bitmap);
        });
    }

    private void SetBackgroundBrush(IBrush brush, Bitmap? associatedBitmap)
    {
        var previousBitmap = _currentBackgroundBitmap;
        _currentBackgroundBitmap = associatedBitmap;

        Background = brush;

        if (!ReferenceEquals(previousBitmap, associatedBitmap))
            previousBitmap?.Dispose();
    }

    private void ApplyTheme(int themeIndex)
    {
        var targetVariant = themeIndex switch
        {
            1 => ThemeVariant.Light,
            2 => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };

        if (Application.Current is { } app && app.RequestedThemeVariant != targetVariant)
            app.RequestedThemeVariant = targetVariant;

        if (Application.Current is App easyApp)
            easyApp.ApplyThemeResources(targetVariant);

        if (RequestedThemeVariant != targetVariant)
            RequestedThemeVariant = targetVariant;
    }

    private static async Task<Bitmap?> LoadBackgroundBitmapAsync(string path)
    {
        try
        {
            if (Uri.TryCreate(path, UriKind.Absolute, out var uri))
            {
                if (uri.IsFile)
                {
                    var localPath = uri.LocalPath;
                    if (!File.Exists(localPath))
                        return null;

                    return await Task.Run(() => new Bitmap(localPath));
                }

                if (uri.Scheme is "http" or "https")
                {
                    var bytes = await BackgroundHttpClient.GetByteArrayAsync(uri);
                    return await Task.Run(() => new Bitmap(new MemoryStream(bytes)));
                }

                return null;
            }

            if (!File.Exists(path))
                return null;

            return await Task.Run(() => new Bitmap(path));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load background image from '{path}': {ex}");
            return null;
        }
    }

    private void SetVersionText()
    {
        if (_versionTextBlock is null)
            return;

        CancelVersionStatusReset();

        var version = VersionProvider.GetApplicationVersion();
        if (string.IsNullOrWhiteSpace(version))
            version = _settings.Update?.CurrentVersion;

        version = version?.Trim();

        if (string.IsNullOrWhiteSpace(version))
        {
            _currentVersionDisplay = null;
            _versionTextBlock.Text = UnknownVersionLabel;
            return;
        }

        _currentVersionDisplay = version;
        if (_settings.Update is null)
            _settings.Update = new UpdateSettings();
        _settings.Update.CurrentVersion = version;
        _versionTextBlock.Text = $"Version {version}";
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        Classes.CollectionChanged -= OnClassesChanged;
        _responsiveLayoutSubscription?.Dispose();
        _responsiveLayoutSubscription = null;
        DiscordRpcService.Instance.Dispose();
        _currentBackgroundBitmap?.Dispose();
        _currentBackgroundBitmap = null;
    }

    private void OnClassesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ApplyResponsiveLayouts();
    }

    private void ApplyResponsiveLayouts()
    {
        var isCompact = Classes.Contains("compact");

        if (_mainContentGrid is not null)
            _mainContentGrid.ColumnDefinitions =
                new ColumnDefinitions(isCompact ? "*" : "2*,*");

        if (_heroGrid is not null)
            _heroGrid.ColumnDefinitions = new ColumnDefinitions(isCompact ? "*" : "Auto,*");

        if (_footerGrid is not null)
        {
            if (isCompact)
            {
                _footerGrid.ColumnDefinitions = new ColumnDefinitions("*");
                _footerGrid.RowDefinitions = new RowDefinitions("Auto,Auto,Auto");
            }
            else
            {
                _footerGrid.ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto");
                _footerGrid.RowDefinitions = new RowDefinitions("Auto");
            }
        }
    }

    private void CancelVersionStatusReset()
    {
        if (_versionStatusReset is null)
            return;

        _versionStatusReset.Dispose();
        _versionStatusReset = null;
    }

    private void SetVersionStatusMessage(string? status, TimeSpan? resetAfter = null)
    {
        if (_versionTextBlock is null)
            return;

        string label;
        if (string.IsNullOrWhiteSpace(_currentVersionDisplay))
        {
            label = UnknownVersionLabel;
            _versionTextBlock.Text = string.IsNullOrWhiteSpace(status)
                ? label
                : $"{label} - {status}";
        }
        else
        {
            label = $"Version {_currentVersionDisplay}";
            _versionTextBlock.Text = string.IsNullOrWhiteSpace(status)
                ? label
                : $"{label} - {status}";
        }

        CancelVersionStatusReset();

        if (resetAfter is { } duration && duration > TimeSpan.Zero)
            _versionStatusReset = DispatcherTimer.RunOnce(() =>
            {
                if (_versionTextBlock is null)
                    return;

                if (string.IsNullOrWhiteSpace(_currentVersionDisplay))
                    _versionTextBlock.Text = UnknownVersionLabel;
                else
                    _versionTextBlock.Text = $"Version {_currentVersionDisplay}";

                _versionStatusReset = null;
            }, duration);
    }


    private string? GetCurrentVersionForComparison()
    {
        if (!string.IsNullOrWhiteSpace(_currentVersionDisplay))
            return _currentVersionDisplay;

        var settingsVersion = _settings.Update?.CurrentVersion;
        if (!string.IsNullOrWhiteSpace(settingsVersion))
            return settingsVersion.Trim();

        var assemblyVersion = VersionProvider.GetApplicationVersion();
        return string.IsNullOrWhiteSpace(assemblyVersion) ? null : assemblyVersion.Trim();
    }


    private static bool TryParseVersion(string? value, [NotNullWhen(true)] out Version? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var normalized = value.Trim();

        if (normalized.StartsWith("version", StringComparison.OrdinalIgnoreCase))
            normalized = normalized[7..].Trim();

        if (normalized.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            normalized = normalized[1..].Trim();

        var separatorIndex = normalized.IndexOfAny(new[] { ' ', '-', '+', '_' });
        if (separatorIndex > 0)
            normalized = normalized[..separatorIndex].Trim();

        var length = 0;
        while (length < normalized.Length)
        {
            var c = normalized[length];
            if ((c >= '0' && c <= '9') || c == '.')
            {
                length++;
                continue;
            }

            break;
        }

        if (length <= 0)
            return false;

        normalized = normalized[..length].Trim('.');
        if (string.IsNullOrWhiteSpace(normalized))
            return false;

        return Version.TryParse(normalized, out result);
    }

    private IBrush ResolveDefaultBackgroundBrush()
    {
        if (Application.Current?.Resources.TryGetValue("EasyWindowBackgroundBrush", out var resource) == true &&
            resource is IBrush brush)
            return brush;

        return Background ?? new SolidColorBrush(Colors.Black);
    }
}