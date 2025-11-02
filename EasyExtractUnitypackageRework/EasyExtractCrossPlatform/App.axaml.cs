using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Styling;
using EasyExtractCrossPlatform.Services;

namespace EasyExtractCrossPlatform;

public class App : Application
{
    private static readonly Uri LightThemeUri = new("avares://EasyExtractCrossPlatform/Styles/Theme.Light.axaml");
    private static readonly Uri DarkThemeUri = new("avares://EasyExtractCrossPlatform/Styles/Theme.Dark.axaml");
    private ResourceDictionary? _darkThemeResources;

    private ResourceDictionary? _lightThemeResources;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        ApplyThemeResources(RequestedThemeVariant ?? ThemeVariant.Default);

        if (OperatingSystem.IsWindows())
        {
            _ = EverythingSdkBootstrapper.EnsureInitializedAsync()
                .ContinueWith(task =>
                {
                    if (task.Exception is not null)
                        Debug.WriteLine($"Failed to initialize Everything SDK: {task.Exception}");
                }, TaskScheduler.Default);
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new MainWindow(desktop.Args);

        base.OnFrameworkInitializationCompleted();
    }

    public void ApplyThemeResources(ThemeVariant themeVariant)
    {
        if (Resources is null)
            return;

        var resolvedVariant = ResolveThemeVariant(themeVariant);
        var targetDictionary = resolvedVariant == ThemeVariant.Light
            ? EnsureLightThemeResources()
            : EnsureDarkThemeResources();

        var mergedDictionaries = Resources.MergedDictionaries;

        RemoveThemeDictionary(mergedDictionaries, _lightThemeResources);
        RemoveThemeDictionary(mergedDictionaries, _darkThemeResources);

        if (!mergedDictionaries.Contains(targetDictionary))
            mergedDictionaries.Add(targetDictionary);
    }

    private ThemeVariant ResolveThemeVariant(ThemeVariant themeVariant)
    {
        if (themeVariant != ThemeVariant.Default)
            return themeVariant;

        if (ActualThemeVariant is { } actualVariant)
            return actualVariant;

        var platformTheme = PlatformSettings?.GetColorValues()?.ThemeVariant;

        if (platformTheme is { } platformVariant)
            return platformVariant switch
            {
                PlatformThemeVariant.Light => ThemeVariant.Light,
                PlatformThemeVariant.Dark => ThemeVariant.Dark,
                _ => ThemeVariant.Dark
            };

        return ThemeVariant.Dark;
    }

    private ResourceDictionary EnsureLightThemeResources()
    {
        return _lightThemeResources ??= LoadThemeResources(LightThemeUri);
    }

    private ResourceDictionary EnsureDarkThemeResources()
    {
        return _darkThemeResources ??= LoadThemeResources(DarkThemeUri);
    }

    private static ResourceDictionary LoadThemeResources(Uri source)
    {
        if (AvaloniaXamlLoader.Load(source) is ResourceDictionary dictionary)
            return dictionary;

        throw new InvalidOperationException($"Unable to load theme resources from {source}.");
    }

    private static void RemoveThemeDictionary(IList<IResourceProvider> dictionaries, IResourceProvider? target)
    {
        if (target is null)
            return;

        for (var i = dictionaries.Count - 1; i >= 0; i--)
            if (ReferenceEquals(dictionaries[i], target))
                dictionaries.RemoveAt(i);
    }
}