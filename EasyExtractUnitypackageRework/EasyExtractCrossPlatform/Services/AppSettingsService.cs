using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        var stopwatch = Stopwatch.StartNew();
        AppSettings? resolvedSettings = null;
        var source = "unknown";
        Exception? failure = null;

        LoggingService.LogInformation($"Loading application settings from '{SettingsFilePath}'.");

        try
        {
            if (!File.Exists(SettingsFilePath))
            {
                LoggingService.LogInformation("Settings file not found. Creating default configuration.");
                var defaults = CreateDefault();
                Save(defaults);
                LoggingService.LogInformation("Default settings persisted successfully.");
                resolvedSettings = defaults;
                source = "defaults";
            }
            else
            {
                using var stream = File.OpenRead(SettingsFilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(stream, SerializerOptions) ?? CreateDefault();
                settings.AppTitle = AppSettings.DefaultAppTitle;
                UpdateStoredVersion(settings);
                EnsureWindowPlacementsStorage(settings);
                LoggingService.LogInformation("Settings loaded successfully.");
                resolvedSettings = settings;
                source = "existing";
            }
        }
        catch (Exception ex)
        {
            LastError = ex;
            LoggingService.LogError("Failed to load settings. Falling back to defaults.", ex);
            resolvedSettings = CreateDefault();
            source = "fallback";
            failure = ex;
        }
        finally
        {
            stopwatch.Stop();
            LoggingService.LogPerformance("AppSettingsService.Load", stopwatch.Elapsed,
                details: $"source={source}|status={(failure is null ? "ok" : "failed")}");
            LoggingService.LogMemoryUsage("AppSettingsService.Load");

            if (resolvedSettings is not null)
                LoggingService.ApplySettingsSnapshot(resolvedSettings, "load");
        }

        return resolvedSettings!;
    }

    public static void Save(AppSettings settings)
    {
        settings.AppTitle = AppSettings.DefaultAppTitle;
        var stopwatch = Stopwatch.StartNew();
        var success = false;
        LoggingService.ApplySettingsSnapshot(settings, "save");
        try
        {
            LoggingService.LogInformation($"Saving application settings to '{SettingsFilePath}'.");
            Directory.CreateDirectory(SettingsDirectory);
            var json = JsonSerializer.Serialize(settings, SerializerOptions);
            File.WriteAllText(SettingsFilePath, json);
            LoggingService.LogInformation("Settings saved successfully.");
            success = true;
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Failed to save application settings.", ex);
            throw;
        }
        finally
        {
            stopwatch.Stop();
            LoggingService.LogPerformance("AppSettingsService.Save", stopwatch.Elapsed,
                details: $"status={(success ? "ok" : "failed")}");
            LoggingService.LogMemoryUsage("AppSettingsService.Save");
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
            LastExtractionTime = DateTimeOffset.Now,
            WindowPlacements = new Dictionary<string, WindowPlacementSettings>(StringComparer.OrdinalIgnoreCase)
        };

        UpdateStoredVersion(defaults);
        EnsureWindowPlacementsStorage(defaults);
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

    private static void EnsureWindowPlacementsStorage(AppSettings settings)
    {
        if (settings is null)
            return;

        if (settings.WindowPlacements is null)
        {
            settings.WindowPlacements =
                new Dictionary<string, WindowPlacementSettings>(StringComparer.OrdinalIgnoreCase);
            return;
        }

        if (settings.WindowPlacements.Comparer == StringComparer.OrdinalIgnoreCase)
            return;

        settings.WindowPlacements = new Dictionary<string, WindowPlacementSettings>(settings.WindowPlacements,
            StringComparer.OrdinalIgnoreCase);
    }
}