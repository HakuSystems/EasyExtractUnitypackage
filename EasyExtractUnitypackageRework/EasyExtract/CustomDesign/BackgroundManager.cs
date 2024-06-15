using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using EasyExtract.Config;

namespace EasyExtract.CustomDesign;

public class BackgroundManager : INotifyPropertyChanged
{
    private static BackgroundManager _instance;
    private double _backgroundOpacity;
    private ImageBrush _currentBackground;
    private readonly BetterLogger _logger = new();
    private readonly ConfigHelper ConfigHelper = new();


    private BackgroundManager()
    {
        _currentBackground = new ImageBrush { Stretch = Stretch.UniformToFill };
        _backgroundOpacity = 1.0;
    }

    public static BackgroundManager Instance => _instance ??= new BackgroundManager();

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
            if (_currentBackground != null) _currentBackground.Opacity = value;
            OnPropertyChanged(nameof(BackgroundOpacity));
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;
    public event EventHandler BackgroundChanged;

    public async void UpdateBackground(string imagePath)
    {
        CurrentBackground = new ImageBrush(new BitmapImage(new Uri(imagePath)))
        {
            Opacity = BackgroundOpacity,
            Stretch = Stretch.UniformToFill
        };
        await _logger.LogAsync($"Background updated with image: {imagePath}", "BackgroundManager.cs",
            Importance.Info); // Log background update
    }

    public async void ResetBackground(string defaultBackgroundResource)
    {
        try
        {
            var defaultBrush =
                Application.Current.FindResource(defaultBackgroundResource ?? "BackgroundPrimaryBrush") as Brush;
            if (defaultBrush is ImageBrush imageBrush)
                CurrentBackground = new ImageBrush(imageBrush.ImageSource)
                {
                    Opacity = BackgroundOpacity,
                    Stretch = Stretch.UniformToFill
                };
            else
                CurrentBackground = new ImageBrush { Opacity = BackgroundOpacity, Stretch = Stretch.UniformToFill };
            await _logger.LogAsync("Background reset to default", "BackgroundManager.cs",
                Importance.Info); // Log background reset
        }
        catch (ResourceReferenceKeyNotFoundException ex)
        {
            CurrentBackground = new ImageBrush { Opacity = BackgroundOpacity, Stretch = Stretch.UniformToFill };
            await _logger.LogAsync($"Default background resource not found: {ex.Message}", "BackgroundManager.cs",
                Importance.Warning); // Log resource not found
        }
    }

    public async void UpdateOpacity(double opacity)
    {
        BackgroundOpacity = opacity;
        if (_currentBackground != null) _currentBackground.Opacity = opacity;
        await ConfigHelper.UpdateConfigAsync(new ConfigModel
        {
            Backgrounds = new BackgroundModel { BackgroundOpacity = opacity }
        });
        await _logger.LogAsync($"Background opacity updated to {opacity}", "BackgroundManager.cs",
            Importance.Info); // Log opacity update
    }

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected void OnBackgroundChanged(EventArgs e)
    {
        BackgroundChanged?.Invoke(this, e);
    }
}