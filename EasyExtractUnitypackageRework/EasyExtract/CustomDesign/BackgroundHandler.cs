using EasyExtract.Config;

namespace EasyExtract.CustomDesign;

public class BackgroundHandler
{
    private readonly BackgroundModel _backgroundConfig;

    public string? BackgroundPath
    {
        get => _backgroundConfig.BackgroundPath;
        set
        {
            _backgroundConfig.BackgroundPath = value;
            ConfigHelper.UpdateConfigAsync(new ConfigModel { Backgrounds = _backgroundConfig });
        }
    }

    public void SetBackground(string? background)
    {
        BackgroundPath = background;
    }

    public void SetBackgroundOpacity(double value)
    {
        _backgroundConfig.BackgroundOpacity = value;
        ConfigHelper.UpdateConfigAsync(new ConfigModel { Backgrounds = _backgroundConfig });
    }

    public object? GetBackground()
    {
        return ConfigHelper.LoadConfigAsync().Result.Backgrounds?.BackgroundPath;
    }

    public object? GetDefaultBackground()
    {
        return ConfigHelper.LoadConfigAsync().Result.Backgrounds?.DefaultBackgroundResource;
    }

    public double GetBackgroundOpacity()
    {
        return ConfigHelper.LoadConfigAsync().Result.Backgrounds?.BackgroundOpacity ?? 0.5;
    }
}