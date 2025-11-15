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
        WriteIndented = true,
        Converters = { new HistoryEntryListJsonConverter() }
    };

    public static string SettingsDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EasyExtract");

    public static string SettingsFilePath { get; } = Path.Combine(SettingsDirectory, "settings.json");

    public static Exception? LastError { get; private set; }

    public static AppSettings Load()
    {
        LastError = null;

        LoggingService.LogInformation($"Loading application settings from '{SettingsFilePath}'.");

        try
        {
            if (!File.Exists(SettingsFilePath))
            {
                LoggingService.LogInformation("Settings file not found. Creating default configuration.");
                var defaults = CreateDefault();
                Save(defaults);
                LoggingService.LogInformation("Default settings persisted successfully.");
                return defaults;
            }

            using var stream = File.OpenRead(SettingsFilePath);
            var settings = JsonSerializer.Deserialize<AppSettings>(stream, SerializerOptions) ?? CreateDefault();
            settings.AppTitle = AppSettings.DefaultAppTitle;
            UpdateStoredVersion(settings);
            LoggingService.LogInformation("Settings loaded successfully.");
            return settings;
        }
        catch (Exception ex)
        {
            LastError = ex;
            LoggingService.LogError("Failed to load settings. Falling back to defaults.", ex);
            return CreateDefault();
        }
    }

    public static void Save(AppSettings settings)
    {
        settings.AppTitle = AppSettings.DefaultAppTitle;
        try
        {
            LoggingService.LogInformation($"Saving application settings to '{SettingsFilePath}'.");
            Directory.CreateDirectory(SettingsDirectory);
            var json = JsonSerializer.Serialize(settings, SerializerOptions);
            File.WriteAllText(SettingsFilePath, json);
            LoggingService.LogInformation("Settings saved successfully.");
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Failed to save application settings.", ex);
            throw;
        }
    }

    public static AppSettings CreateDefault()
    {
        Directory.CreateDirectory(SettingsDirectory);
        LoggingService.LogInformation("Creating default application settings profile.");

        var defaults = new AppSettings
        {
            DefaultOutputPath = Path.Combine(SettingsDirectory, "Extracted"),
            DefaultTempPath = Path.Combine(SettingsDirectory, "Temp"),
            ApplicationTheme = 0,
            ContextMenuToggle = true,
            DiscordRpc = true,
            ExtractedCategoryStructure = false,
            EnableSecurityScanning = false,
            EnableSound = true,
            SoundVolume = 1.0,
            CustomBackgroundImage = new CustomBackgroundImageSettings
            {
                IsEnabled = false
            },
            FirstRun = false,
            LastExtractionTime = DateTimeOffset.Now
        };

        UpdateStoredVersion(defaults);
        LoggingService.LogInformation(
            $"Default settings created with output '{defaults.DefaultOutputPath}' and temp '{defaults.DefaultTempPath}'.");
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
        LoggingService.LogInformation($"Recorded application version '{version}' in settings.");
    }
}