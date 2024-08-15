using System.IO;
using Newtonsoft.Json;

namespace EasyExtract.Config;

public class ConfigHelper
{
    private static readonly string ConfigPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EasyExtract",
            "Settings.json");

    private static readonly SemaphoreSlim Semaphore = new(1, 1);
    private readonly BetterLogger _logger = new();

    public ConfigHelper()
    {
        if (File.Exists(ConfigPath))
            Task.Run(async () => await ReadConfigAsync());
        else
            Task.Run(async () => await UpdateConfigAsync());
    }

    public ConfigModel Config { get; private set; } = new();

    public async Task ReadConfigAsync()
    {
        try
        {
            var json = await File.ReadAllTextAsync(ConfigPath).ConfigureAwait(false);
            Config = JsonConvert.DeserializeObject<ConfigModel>(json);
            await _logger.LogAsync("Read config file", "ConfigHelper", Importance.Debug);
        }
        catch (Exception e)
        {
            await _logger.LogAsync($"Exception in ReadConfigAsync: {e.Message}", "ConfigHelper", Importance.Error);
        }
    }


    public async Task UpdateConfigAsync()
    {
        await Semaphore.WaitAsync();
        try
        {
            var json = JsonConvert.SerializeObject(Config, Formatting.Indented);
            await using var sw = new StreamWriter(ConfigPath, false);
            await sw.WriteAsync(json);
            await _logger.LogAsync($"Updated config file: {json}", "ConfigHelper", Importance.Debug);
        }
        catch (Exception e)
        {
            await _logger.LogAsync($"Exception in UpdateConfigAsync: {e.Message}", "ConfigHelper", Importance.Error);
        }
        finally
        {
            Semaphore.Release();
        }
    }
}