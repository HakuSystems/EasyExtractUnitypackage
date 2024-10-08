using System.Text;
using System.Windows.Media;
using EasyExtract.Config;
using EasyExtract.Utilities;

namespace EasyExtract.UI.CustomDesign;

public class BackgroundManager : INotifyPropertyChanged
{
    private static BackgroundManager? _instance;
    private readonly ConfigHelper _configHelper = new();
    private double _backgroundOpacity;
    private ImageBrush _currentBackground;


    private BackgroundManager()
    {
        _currentBackground = new ImageBrush
        {
            Stretch = Stretch.Fill
        };
        _backgroundOpacity = 1.0;
    }

    public static BackgroundManager Instance
    {
        get => _instance ??= new BackgroundManager();
    }

    public ImageBrush CurrentBackground
    {
        get => _currentBackground;
        set
        {
            _currentBackground = value;
            OnPropertyChanged(nameof(CurrentBackground));
            OnBackgroundChanged(EventArgs.Empty);
        }
    }

    public double BackgroundOpacity
    {
        get => _backgroundOpacity;
        set
        {
            _backgroundOpacity = value;
            _currentBackground.Opacity = value;
            OnPropertyChanged(nameof(BackgroundOpacity));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? BackgroundChanged;

    public async Task UpdateBackground(string imagePath)
    {
        if (string.IsNullOrEmpty(imagePath))
        {
            await ResetBackground();
            return;
        }

        CurrentBackground = new ImageBrush(new BitmapImage(new Uri(imagePath)))
        {
            Opacity = BackgroundOpacity,
            Stretch = Stretch.Fill
        };
        await BetterLogger.LogAsync($"Background updated with image: {imagePath}", $"{nameof(BackgroundManager)}.cs",
            Importance.Info); // Log background update
    }

    public async Task ResetBackground()
    {
        try
        {
            var uri = new Uri(new StringBuilder().Append(
                    "\u0068\u0074\u0074\u0070\u0073\u003a\u002f\u002f\u0072\u0061\u0077\u002e\u0067\u0069\u0074\u0068\u0075\u0062\u0075\u0073\u0065\u0072\u0063\u006f\u006e\u0074\u0065\u006e\u0074\u002e\u0063\u006f\u006d\u002f\u0048\u0061\u006b\u0075\u0053\u0079\u0073\u0074\u0065\u006d\u0073\u002f\u0047\u0072\u0061\u0070\u0068\u0069\u0063\u0073\u0053\u0074\u0075\u0066\u0066\u002f\u006d\u0061\u0069\u006e\u002f\u0045\u0061\u0073\u0079\u0045\u0078\u0074\u0072\u0061\u0063\u0074\u0055\u006e\u0069\u0074\u0079\u0070\u0061\u0063\u006b\u0061\u0067\u0065\u005f\u0042\u0061\u0063\u006b\u0067\u0072\u006f\u0075\u006e\u0064\u0025\u0032\u0030\u0038\u004b\u002e\u0070\u006e\u0067")
                .ToString());
            CurrentBackground = new ImageBrush(new BitmapImage(new Uri(uri.ToString())))
            {
                Opacity = BackgroundOpacity,
                Stretch = Stretch.Fill
            };
            _configHelper.Config.Backgrounds.BackgroundPath = uri.ToString();
            await UpdateBackground(uri.ToString());
            await _configHelper.UpdateConfigAsync();
            await BetterLogger.LogAsync("Background reset to default", "BackgroundManager.cs",
                Importance.Info); // Log background reset
        }
        catch (ResourceReferenceKeyNotFoundException ex)
        {
            CurrentBackground = new ImageBrush
            {
                Opacity = BackgroundOpacity,
                Stretch = Stretch.Fill
            };
            await BetterLogger.LogAsync($"Default background resource not found: {ex.Message}", "BackgroundManager.cs",
                Importance.Warning); // Log resource not found
        }
    }

    public async Task UpdateOpacity(double opacity)
    {
        BackgroundOpacity = opacity;
        _currentBackground.Opacity = opacity;
        _configHelper.Config.Backgrounds.BackgroundOpacity = opacity;
        await _configHelper.UpdateConfigAsync();
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void OnBackgroundChanged(EventArgs e)
    {
        BackgroundChanged?.Invoke(this, e);
    }
}