using EasyExtract.Config;
using EasyExtract.Config.Models;
using EasyExtract.Utilities;

namespace EasyExtract.Services;

public class ThemeService
{
    private readonly ColorConverter _colorConverter;
    private readonly ResourceDictionary _colorsDictionary;
    private readonly ConfigModel _config;

    public ThemeService(ConfigModel config)
    {
        _config = config;
        _colorConverter = new ColorConverter();

        _colorsDictionary = Application.Current.Resources.MergedDictionaries.FirstOrDefault(d =>
            d.Source != null && d.Source.OriginalString.EndsWith("Colors.xaml"));

        if (_colorsDictionary == null)
        {
            _ = BetterLogger.LogAsync("Colors.xaml not found in Application Resources.", Importance.Warning);
            return;
        }

        _config.PropertyChanged += OnConfigPropertyChanged;
        ApplyThemeFromConfig();
    }

    private void OnConfigPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(_config.TextColorHex)
            || e.PropertyName == nameof(_config.BackgroundColorHex)
            || e.PropertyName == nameof(_config.PrimaryColorHex)
            || e.PropertyName == nameof(_config.SecondaryColorHex)
            || e.PropertyName == nameof(_config.AccentColorHex))
            ApplyThemeFromConfig();
    }

    public void ApplyThemeFromConfig()
    {
        if (_colorsDictionary == null)
            return;

        try
        {
            // Use the _colorConverter instance
            _colorsDictionary["TextColor"] = (Color)_colorConverter.ConvertFromString(_config.TextColorHex);
            _colorsDictionary["BackgroundColor"] = (Color)_colorConverter.ConvertFromString(_config.BackgroundColorHex);
            _colorsDictionary["PrimaryColor"] = (Color)_colorConverter.ConvertFromString(_config.PrimaryColorHex);
            _colorsDictionary["SecondaryColor"] = (Color)_colorConverter.ConvertFromString(_config.SecondaryColorHex);
            _colorsDictionary["AccentColor"] = (Color)_colorConverter.ConvertFromString(_config.AccentColorHex);
        }
        catch (Exception ex)
        {
            _ = BetterLogger.LogAsync($"Error applying theme: {ex.Message}", Importance.Error, ex);
        }
    }
}