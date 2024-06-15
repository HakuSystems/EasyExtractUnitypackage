using EasyExtract.Config;

namespace EasyExtract.CustomDesign;

public class BackgroundHandler
{
    private readonly BackgroundModel _backgroundConfig;
    private readonly BetterLogger _logger = new();
    private readonly ConfigHelper ConfigHelper = new();

    public BackgroundHandler(BackgroundModel backgroundConfig)
    {
        _backgroundConfig = backgroundConfig;
    }

    public string? BackgroundPath
    {
        get => _backgroundConfig.BackgroundPath;
        set
        {
            _backgroundConfig.BackgroundPath = value;
            ConfigHelper.UpdateConfigAsync(new ConfigModel { Backgrounds = _backgroundConfig }).Wait();
        }
    }

    public async void SetBackground(string? background)
    {
        BackgroundPath = background;
        await _logger.LogAsync($"Set background to: {background}", "BackgroundHandler.cs",
            Importance.Info); // Log set background
    }

    public async void SetBackgroundOpacity(double value) // not used
    {
        _backgroundConfig.BackgroundOpacity = value;
        await ConfigHelper.UpdateConfigAsync(new ConfigModel { Backgrounds = _backgroundConfig });
        await _logger.LogAsync($"Background opacity set to: {value}", "BackgroundHandler.cs",
            Importance.Info); // Log background opacity set
    }

    public async Task<object?> GetBackground()
    {
        var backgroundPath = ConfigHelper.ReadConfigAsync().Result.Backgrounds?.BackgroundPath;
        await _logger.LogAsync($"Retrieved background path: {backgroundPath}", "BackgroundHandler.cs",
            Importance.Info); // Log get background
        return backgroundPath;
    }

    public async Task<object?> GetDefaultBackground()
    {
        var defaultBackground = ConfigHelper.ReadConfigAsync().Result.Backgrounds?.DefaultBackgroundResource;
        await _logger.LogAsync($"Retrieved default background: {defaultBackground}", "BackgroundHandler.cs",
            Importance.Info); // Log get default background
        return defaultBackground;
    }

    public async Task<double> GetBackgroundOpacity()
    {
        var opacity = ConfigHelper.ReadConfigAsync().Result.Backgrounds?.BackgroundOpacity ?? 0.5;
        await _logger.LogAsync($"Retrieved background opacity: {opacity}", "BackgroundHandler.cs",
            Importance.Info); // Log get background opacity
        return opacity;
    }
}