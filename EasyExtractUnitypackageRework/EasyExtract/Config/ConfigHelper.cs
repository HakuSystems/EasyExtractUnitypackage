using AutoMapper;
using EasyExtract.Utilities.Logger;
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
    private static readonly IMapper _mapper = new MapperConfiguration(cfg =>
    {
        // Map Config -> Config for easy property transfer if needed
        cfg.CreateMap<ConfigModel, ConfigModel>();
    }).CreateMapper();

    private readonly TimeSpan _debounceDelay = TimeSpan.FromMilliseconds(500);

    // Use a debounce mechanism to avoid too many saves
    private readonly object _saveLock = new();

    // We use BetterLogger for logging
    private bool _initialized;
    private bool _saveScheduled;

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
    ///     Only initialize once; the following calls do nothing.
    /// </summary>
    public async Task InitializeIfNeededAsync()
    {
        if (_initialized) return;
        _initialized = true;

        BetterLogger.LogWithContext("Initializing ConfigHandler", new Dictionary<string, object>(), LogLevel.Info,
            "Config");

        if (File.Exists(ConfigPath))
        {
            BetterLogger.LogWithContext("Reading existing config file",
                new Dictionary<string, object> { ["ConfigPath"] = ConfigPath }, LogLevel.Info, "Config");
            await ReadConfigAsync();
        }
        else
        {
            BetterLogger.LogWithContext("Creating new config file",
                new Dictionary<string, object> { ["ConfigPath"] = ConfigPath }, LogLevel.Warning, "Config");
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath) ?? string.Empty);
            await UpdateConfigAsync(); // Creates a file with defaults
        }

        BetterLogger.LogWithContext("ConfigHandler initialization completed", new Dictionary<string, object>(),
            LogLevel.Info, "Config");
        await GenerateAllNecessaryFiles();
    }

    private static Task GenerateAllNecessaryFiles()
    {
        //Appdata folder
        var appdata = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appdataFolder = Path.Combine(appdata, "EasyExtract");
        if (!Directory.Exists(appdataFolder)) Directory.CreateDirectory(appdataFolder);

        //EasyExtract\Extracted
        var extractedFolder = Path.Combine(appdataFolder, "Extracted");
        if (!Directory.Exists(extractedFolder)) Directory.CreateDirectory(extractedFolder);

        //EasyExtract\Temp
        var tempFolder = Path.Combine(appdataFolder, "Temp");
        if (!Directory.Exists(tempFolder)) Directory.CreateDirectory(tempFolder);

        //EasyExtract\ThirdParty
        var thirdPartyFolder = Path.Combine(appdataFolder, "ThirdParty");
        if (!Directory.Exists(thirdPartyFolder)) Directory.CreateDirectory(thirdPartyFolder);

        //EasyExtract\IgnoredUnity packages
        var ignoredUnityPackagesFolder = Path.Combine(appdataFolder, "IgnoredUnitypackages");
        if (!Directory.Exists(ignoredUnityPackagesFolder)) Directory.CreateDirectory(ignoredUnityPackagesFolder);
        return Task.CompletedTask;
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
                BetterLogger.LogWithContext("Config deserialized successfully", new Dictionary<string, object>(),
                    LogLevel.Info, "Config");
                UpdateConfigProperties(configFromFile);
            }
            else
            {
                BetterLogger.LogWithContext("Config file was empty or invalid", new Dictionary<string, object>(),
                    LogLevel.Warning, "Config");
            }
        }
        catch (Exception ex)
        {
            BetterLogger.Exception(ex, "Error reading config", "Config");
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
        }
        catch (Exception ex)
        {
            BetterLogger.Exception(ex, "Error updating config", "Config");
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
    ///     Whenever a property on Config changes, automatically save it (if initialized) with debouncing.
    /// </summary>
    private void Config_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_initialized) return;

        lock (_saveLock)
        {
            if (!_saveScheduled)
            {
                _saveScheduled = true;
                Task.Delay(_debounceDelay).ContinueWith(async _ =>
                {
                    await UpdateConfigAsync();
                    lock (_saveLock)
                    {
                        _saveScheduled = false;
                    }
                });
            }
        }
    }

    /// <summary>
    ///     Force an immediate save of the current Config to disk.
    /// </summary>
    public async Task OverrideConfigAsync()
    {
        await UpdateConfigAsync();
    }

    /// <summary>
    ///     Force an immediate save of the current Config to disk (non-async version).
    /// </summary>
    public void OverrideConfig()
    {
        // For backward compatibility
        Task.Run(async () => await OverrideConfigAsync()).ConfigureAwait(false);
    }
}