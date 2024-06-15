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

    public void UpdateBackground(string imagePath)
    {
        CurrentBackground = new ImageBrush(new BitmapImage(new Uri(imagePath)))
        {
            Opacity = BackgroundOpacity,
            Stretch = Stretch.UniformToFill
        };
    }

    public void ResetBackground(string defaultBackgroundResource)
    {
        try
        {
            var defaultBrush = Application.Current.FindResource(defaultBackgroundResource) as Brush;
            if (defaultBrush is ImageBrush imageBrush)
                CurrentBackground = new ImageBrush(imageBrush.ImageSource)
                {
                    Opacity = BackgroundOpacity,
                    Stretch = Stretch.UniformToFill
                };
            else
                CurrentBackground = new ImageBrush { Opacity = BackgroundOpacity, Stretch = Stretch.UniformToFill };
        }
        catch (ResourceReferenceKeyNotFoundException)
        {
            CurrentBackground = new ImageBrush { Opacity = BackgroundOpacity, Stretch = Stretch.UniformToFill };
        }
    }

    public void UpdateOpacity(double opacity)
    {
        BackgroundOpacity = opacity;
        if (_currentBackground != null) _currentBackground.Opacity = opacity;
        Task.Run(() => ConfigHelper.UpdateConfigAsync(new ConfigModel
            { Backgrounds = new BackgroundModel { BackgroundOpacity = opacity } }));
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