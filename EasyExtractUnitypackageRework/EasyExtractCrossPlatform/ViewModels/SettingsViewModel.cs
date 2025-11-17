using System;
using System.Collections.Generic;
using EasyExtractCrossPlatform.Localization;
using EasyExtractCrossPlatform.Models;
using EasyExtractCrossPlatform.Services;
using EasyExtractCrossPlatform.Utilities;

namespace EasyExtractCrossPlatform.ViewModels;

public class SettingsViewModel
{
    private const double BytesPerMegabyte = 1024d * 1024d;
    private const double BytesPerGigabyte = BytesPerMegabyte * 1024d;
    private const double MinAssetMegabytes = 16d;
    private const double MinPackageGigabytes = 1d;
    private const double MinAssetCount = 100d;

    public SettingsViewModel(AppSettings settings, string? loadError = null)
    {
        Settings = settings;
        LoadErrorMessage = loadError;
        var version = VersionProvider.GetApplicationVersion();
        CurrentVersion = string.IsNullOrWhiteSpace(version)
            ? settings.Update.CurrentVersion
            : version;

        ThemeOptions = new List<SelectionOption>
        {
            new(0, LocalizationManager.Instance.GetString("SettingsWindow_ThemeFollowSystem")),
            new(1, LocalizationManager.Instance.GetString("SettingsWindow_ThemeLight")),
            new(2, LocalizationManager.Instance.GetString("SettingsWindow_ThemeDark"))
        };
    }

    public AppSettings Settings { get; }

    public IReadOnlyList<SelectionOption> ThemeOptions { get; }

    public string? LoadErrorMessage { get; }

    public string RepositoryOwner => Settings.Update.RepoOwner ?? string.Empty;

    public string RepositoryName => Settings.Update.RepoName ?? string.Empty;

    public string CurrentVersion { get; }

    public double ExtractionMaxAssetMegabytes
    {
        get => Math.Round(EnsureExtractionLimits().MaxAssetBytes / BytesPerMegabyte, 2);
        set => EnsureExtractionLimits().MaxAssetBytes = ToBytes(
            value,
            MinAssetMegabytes,
            UnityPackageExtractionLimits.MaxAllowedAssetBytes,
            BytesPerMegabyte);
    }

    public double ExtractionMaxPackageGigabytes
    {
        get => Math.Round(EnsureExtractionLimits().MaxPackageBytes / BytesPerGigabyte, 2);
        set => EnsureExtractionLimits().MaxPackageBytes = ToBytes(
            value,
            MinPackageGigabytes,
            UnityPackageExtractionLimits.MaxAllowedPackageBytes,
            BytesPerGigabyte);
    }

    public double ExtractionMaxAssetCount
    {
        get => EnsureExtractionLimits().MaxAssets;
        set
        {
            var sanitized = double.IsNaN(value) || double.IsInfinity(value) ? MinAssetCount : value;
            var clamped = (int)Math.Clamp(Math.Round(sanitized, MidpointRounding.AwayFromZero), MinAssetCount,
                UnityPackageExtractionLimits.MaxAllowedAssets);
            EnsureExtractionLimits().MaxAssets = clamped;
        }
    }

    public static SettingsViewModel CreateFromStorage()
    {
        var settings = AppSettingsService.Load();
        return new SettingsViewModel(settings, AppSettingsService.LastError?.Message);
    }

    public void ResetExtractionLimits()
    {
        Settings.ExtractionLimits = UnityPackageExtractionLimits.Normalize(UnityPackageExtractionLimits.Default);
    }

    private UnityPackageExtractionLimits EnsureExtractionLimits()
    {
        Settings.ExtractionLimits = UnityPackageExtractionLimits.Normalize(Settings.ExtractionLimits);
        return Settings.ExtractionLimits;
    }

    private static long ToBytes(double value, double minUnits, long maxBytes, double unitSize)
    {
        var sanitized = double.IsNaN(value) || double.IsInfinity(value) ? minUnits : value;
        var maxUnits = maxBytes / unitSize;
        var clamped = Math.Clamp(sanitized, minUnits, maxUnits);
        return (long)Math.Round(clamped * unitSize, MidpointRounding.AwayFromZero);
    }
}

public record SelectionOption(int Value, string DisplayName);