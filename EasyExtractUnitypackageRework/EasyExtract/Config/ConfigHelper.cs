using AutoMapper;
using EasyExtract.Config.Models;
using EasyExtract.Utilities;
using Newtonsoft.Json;

namespace EasyExtract.Config;

public class ConfigHandler
{
    // The configuration file path is set to AppData/EasyExtract
    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "EasyExtract",
        "Settings.json"
    );

    private static readonly SemaphoreSlim Semaphore = new(1, 1);
    private static readonly Lazy<ConfigHandler> _instance = new(() => new ConfigHandler());

    // Optional: Use AutoMapper to map from loaded Config to the in-memory Config
    private static readonly IMapper _mapper;

    // We use BetterLogger for logging
    private bool _initialized;

    static ConfigHandler()
    {
        var mapperConfig = new MapperConfiguration(cfg =>
        {
            // Map Config -> Config for easy property transfer if needed
            cfg.CreateMap<ConfigModel, ConfigModel>();
        });
        _mapper = mapperConfig.CreateMapper();
    }

    private ConfigHandler()
    {
        // Create default instance
        Config = new ConfigModel();
        Config.PropertyChanged += Config_PropertyChanged; // auto-save on property changes
    }

    /// <summary>
    ///     Singleton instance: call ConfigHandler.Instance to access the config.
    /// </summary>
    public static ConfigHandler Instance => _instance.Value;

    public ConfigModel Config { get; }

    /// <summary>
    ///     Only initialize once; subsequent calls do nothing.
    /// </summary>
    public async Task InitializeIfNeededAsync()
    {
        if (_initialized) return;
        _initialized = true;

        await BetterLogger.LogAsync("Initializing ConfigHandler...", Importance.Info);

        if (File.Exists(ConfigPath))
        {
            await BetterLogger.LogAsync($"Config file found at {ConfigPath}. Reading config...", Importance.Info);
            await ReadConfigAsync();
        }
        else
        {
            await BetterLogger.LogAsync($"No config file found. Creating a new one at {ConfigPath}...",
                Importance.Warning);
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));
            await UpdateConfigAsync(); // Creates file with defaults
        }

        await BetterLogger.LogAsync("ConfigHandler initialization completed.", Importance.Info);
    }

    /// <summary>
    ///     Reads config from disk (JSON) and applies values to the in-memory Config object.
    /// </summary>
    private async Task ReadConfigAsync()
    {
        await Semaphore.WaitAsync();
        try
        {
            var json = await File.ReadAllTextAsync(ConfigPath).ConfigureAwait(false);
            var configFromFile = JsonConvert.DeserializeObject<ConfigModel>(json);

            if (configFromFile != null)
            {
                await BetterLogger.LogAsync("Config successfully deserialized. Updating in-memory Config...",
                    Importance.Info);
                UpdateConfigProperties(configFromFile);
            }
            else
            {
                await BetterLogger.LogAsync("Config file was empty or invalid. Using default config.",
                    Importance.Warning);
            }
        }
        catch (Exception ex)
        {
            await BetterLogger.LogAsync($"Error reading config: {ex.Message}", Importance.Error, ex);
        }
        finally
        {
            Semaphore.Release();
        }
    }

    /// <summary>
    ///     Saves current config to disk as JSON.
    /// </summary>
    private async Task UpdateConfigAsync()
    {
        await Semaphore.WaitAsync();
        try
        {
            var json = JsonConvert.SerializeObject(Config, Formatting.Indented);
            await File.WriteAllTextAsync(ConfigPath, json).ConfigureAwait(false);
            await BetterLogger.LogAsync($"Updated config file at {ConfigPath}", Importance.Debug);
        }
        catch (Exception ex)
        {
            await BetterLogger.LogAsync($"Error updating config: {ex.Message}", Importance.Error, ex);
        }
        finally
        {
            Semaphore.Release();
        }
    }

    /// <summary>
    ///     Maps all properties from the loaded Config onto the existing in-memory Config.
    ///     This preserves the same instance but updates its fields.
    /// </summary>
    private void UpdateConfigProperties(ConfigModel source)
    {
        // Temporarily detach event handler to avoid repeated saves
        Config.PropertyChanged -= Config_PropertyChanged;
        try
        {
            // If using AutoMapper
            _mapper.Map(source, Config);
        }
        finally
        {
            // Re-attach event handler
            Config.PropertyChanged += Config_PropertyChanged;
        }
    }

    /// <summary>
    ///     Whenever a property on Config changes, automatically save it (if initialized).
    /// </summary>
    private void Config_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (!_initialized) return;
        _ = UpdateConfigAsync(); // fire-and-forget
    }

    /// <summary>
    ///     Force an immediate save of the current Config to disk.
    /// </summary>
    public void OverrideConfig()
    {
        _ = UpdateConfigAsync();
    }
}