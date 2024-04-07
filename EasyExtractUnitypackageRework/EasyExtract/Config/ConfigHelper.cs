using System.IO;
using Newtonsoft.Json;

namespace EasyExtract.Config;

public class ConfigHelper
{
    private static readonly string _configPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EasyExtract",
            "Settings.json");

    private static readonly SemaphoreSlim _semaphore = new(1, 1);

    private static async Task CreateConfig()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_configPath));
        Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "EasyExtract", "Temp"));
        Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "EasyExtract", "Extracted"));
        await using var sw = new StreamWriter(_configPath, false);
        await sw.WriteAsync(JsonConvert.SerializeObject(new ConfigModel(), Formatting.Indented));
    }

    public static async Task<ConfigModel?> LoadConfig()
    {
        await _semaphore.WaitAsync();
        try
        {
            if (!File.Exists(_configPath)) await CreateConfig();
            using (var sr = new StreamReader(_configPath))
            {
                var fileContents = await sr.ReadToEndAsync();
                return JsonConvert.DeserializeObject<ConfigModel>(fileContents);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public static async Task UpdateConfig(ConfigModel? config)
    {
        await _semaphore.WaitAsync();
        try
        {
            if (!File.Exists(_configPath)) await CreateConfig();
            using (var sw = new StreamWriter(_configPath, false))
            {
                var json = JsonConvert.SerializeObject(config, Formatting.Indented);
                await sw.WriteAsync(json);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public static async Task AddToHistory(HistoryModel history)
    {
        var config = await LoadConfig();
        if (config == null) return;
        config.History.Add(history);
        await UpdateConfig(config);
    }

    public static Task ResetConfig()
    {
        if (File.Exists(_configPath)) File.Delete(_configPath);
        return Task.CompletedTask;
    }
}