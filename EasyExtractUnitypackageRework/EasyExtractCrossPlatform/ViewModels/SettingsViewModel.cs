using System.Collections.Generic;
using EasyExtractCrossPlatform.Models;
using EasyExtractCrossPlatform.Services;
using EasyExtractCrossPlatform.Utilities;

namespace EasyExtractCrossPlatform.ViewModels;

public class SettingsViewModel
{
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
            new(0, "Follow system"),
            new(1, "Light"),
            new(2, "Dark")
        };
    }

    public AppSettings Settings { get; }

    public IReadOnlyList<SelectionOption> ThemeOptions { get; }

    public string? LoadErrorMessage { get; }

    public string RepositoryOwner => Settings.Update.RepoOwner ?? string.Empty;

    public string RepositoryName => Settings.Update.RepoName ?? string.Empty;

    public string CurrentVersion { get; }

    public static SettingsViewModel CreateFromStorage()
    {
        var settings = AppSettingsService.Load();
        return new SettingsViewModel(settings, AppSettingsService.LastError?.Message);
    }
}

public record SelectionOption(int Value, string DisplayName);