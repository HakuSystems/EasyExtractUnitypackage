using System.IO;
using Newtonsoft.Json;

namespace EasyExtract.Config;

public class ConfigHelper
{
    private static readonly string _configPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EasyExtract",
            "Settings.json");

    private static readonly SemaphoreSlim Semaphore = new(1, 1);
    private ConfigModel? _config;
    private readonly BetterLogger _logger = new();

    private async Task CreateConfigAsync()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_configPath));
        _config = new ConfigModel();
        await _logger.LogAsync("Created new config file", "ConfigHelper", Importance.Info);
        await SaveConfigAsync(_config);
    }

    public async Task<ConfigModel?> ReadConfigAsync()
    {
        await Semaphore.WaitAsync();
        try
        {
            if (_config != null) return _config;
            if (!File.Exists(_configPath)) await CreateConfigAsync();
            using var sr = new StreamReader(_configPath);
            var fileContents = await sr.ReadToEndAsync();
            _config = JsonConvert.DeserializeObject<ConfigModel>(fileContents) ?? new ConfigModel();
            await _logger.LogAsync("Read config file", "ConfigHelper", Importance.Info);
            return _config;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return null;
        }
        finally
        {
            Semaphore.Release();
        }
    }

    public async Task UpdateConfigAsync(ConfigModel? config)
    {
        await Semaphore.WaitAsync();
        try
        {
            _config = config;
            await _logger.LogAsync("Updated config file", "ConfigHelper", Importance.Info);
            await SaveConfigAsync(_config);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
        finally
        {
            Semaphore.Release();
        }
    }

    private async Task SaveConfigAsync(ConfigModel? config)
    {
        await using var sw = new StreamWriter(_configPath, false);
        var json = JsonConvert.SerializeObject(config, Formatting.Indented);
        await sw.WriteAsync(json);
        await _logger.LogAsync("Saved config file", "ConfigHelper", Importance.Info);
    }
}