using System;
using Avalonia.Data;
using Avalonia.Markup.Xaml;

namespace EasyExtractCrossPlatform.Localization;

/// <summary>
///     Markup extension that resolves a localized string at runtime.
///     Usage: Text="{loc:Loc MainWindow_Title}".
/// </summary>
public class LocExtension : MarkupExtension
{
    public LocExtension()
    {
    }

    public LocExtension(string key)
    {
        Key = key;
    }

    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var key = Key;
        if (string.IsNullOrWhiteSpace(key)) return string.Empty;

        var binding = new Binding
        {
            Source = LocalizationManager.Instance,
            Path = $"[{key}]",
            Mode = BindingMode.OneWay
        };

        return binding;
    }
}