using System;
using System.IO;
using System.Text.Json;
using EasyExtractCrossPlatform.Models;
using EasyExtractCrossPlatform.Utilities;

namespace EasyExtractCrossPlatform.Services;

public static class AppSettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    public static string SettingsDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EasyExtract");

    public static string SettingsFilePath { get; } = Path.Combine(SettingsDirectory, "settings.json");

    public static Exception? LastError { get; private set; }

    public static AppSettings Load()
    {
        LastError = null;

        try
        {
            if (!File.Exists(SettingsFilePath))
            {
                var defaults = CreateDefault();
                Save(defaults);
                return defaults;
            }

            using var stream = File.OpenRead(SettingsFilePath);
            var settings = JsonSerializer.Deserialize<AppSettings>(stream, SerializerOptions) ?? CreateDefault();
            UpdateStoredVersion(settings);
            return settings;
        }
        catch (Exception ex)
        {
            LastError = ex;
            return CreateDefault();
        }
    }

    public static void Save(AppSettings settings)
    {
        Directory.CreateDirectory(SettingsDirectory);
        var json = JsonSerializer.Serialize(settings, SerializerOptions);
        File.WriteAllText(SettingsFilePath, json);
    }

    public static AppSettings CreateDefault()
    {
        Directory.CreateDirectory(SettingsDirectory);

        var defaults = new AppSettings
        {
            DefaultOutputPath = Path.Combine(SettingsDirectory, "Extracted"),
            DefaultTempPath = Path.Combine(SettingsDirectory, "Temp"),
            ApplicationTheme = 0,
            ContextMenuToggle = true,
            DiscordRpc = true,
            ExtractedCategoryStructure = true,
            EnableSound = true,
            SoundVolume = 1.0,
            FirstRun = false,
            LastExtractionTime = DateTimeOffset.Now
        };

        UpdateStoredVersion(defaults);
        return defaults;
    }

    private static void UpdateStoredVersion(AppSettings settings)
    {
        var version = VersionProvider.GetApplicationVersion();
        if (string.IsNullOrWhiteSpace(version))
            return;

        if (settings.Update is null)
            settings.Update = new UpdateSettings();

        settings.Update.CurrentVersion = version;
    }
}