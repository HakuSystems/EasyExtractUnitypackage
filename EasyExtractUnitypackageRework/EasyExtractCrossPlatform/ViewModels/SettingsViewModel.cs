using System.Collections.Generic;
using EasyExtractCrossPlatform.Models;
using EasyExtractCrossPlatform.Services;

namespace EasyExtractCrossPlatform.ViewModels;

public class SettingsViewModel
{
    public SettingsViewModel(AppSettings settings, string? loadError = null)
    {
        Settings = settings;
        LoadErrorMessage = loadError;

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

    public static SettingsViewModel CreateFromStorage()
    {
        var settings = AppSettingsService.Load();
        return new SettingsViewModel(settings, AppSettingsService.LastError?.Message);
    }
}

public record SelectionOption(int Value, string DisplayName);