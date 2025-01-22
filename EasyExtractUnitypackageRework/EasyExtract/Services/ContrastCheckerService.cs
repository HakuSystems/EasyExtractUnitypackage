using EasyExtract.Models;

namespace EasyExtract.Services;

public class ContrastCheckerService
{
    private readonly ColorConverter _colorConverter;
    private readonly ConfigModel _config;

    public ContrastCheckerService(ConfigModel config)
    {
        _config = config;
        _colorConverter = new ColorConverter(); 

        EnsureContrastBindings();
    }

    public void LoadColorsAndCheckContrast()
    {
        var textColor = (Color)_colorConverter.ConvertFromString(_config.TextColorHex);
        var backgroundColor = (Color)_colorConverter.ConvertFromString(_config.BackgroundColorHex);
        var primaryColor = (Color)_colorConverter.ConvertFromString(_config.PrimaryColorHex);
        var secondaryColor = (Color)_colorConverter.ConvertFromString(_config.SecondaryColorHex);
        var accentColor = (Color)_colorConverter.ConvertFromString(_config.AccentColorHex);

        UpdateContrastProperties(
            textColor,
            backgroundColor,
            primaryColor,
            secondaryColor,
            accentColor
        );
    }

    public void UpdateColorsInMemory(
        string textColorHex,
        string backgroundColorHex,
        string primaryColorHex,
        string secondaryColorHex,
        string accentColorHex
    )
    {
        _config.TextColorHex = textColorHex;
        _config.BackgroundColorHex = backgroundColorHex;
        _config.PrimaryColorHex = primaryColorHex;
        _config.SecondaryColorHex = secondaryColorHex;
        _config.AccentColorHex = accentColorHex;

        LoadColorsAndCheckContrast();
    }

    private void UpdateContrastProperties(
        Color textColor,
        Color bgColor,
        Color primary,
        Color secondary,
        Color accent
    )
    {
        var ratio = CalculateContrastRatio(textColor, bgColor);
        _config.CurrentThemeContrastRatio = ratio.ToString("F2");
        _config.CurrentThemeTextContrast = GetContrastStatus(ratio);

        var headlinesRatio = CalculateContrastRatio(textColor, primary);
        _config.CurrentThemeHeadlinesContrast = GetContrastStatus(headlinesRatio);

        var componentsRatio = CalculateContrastRatio(bgColor, accent);
        _config.CurrentThemeComponentsContrast = GetAccentContrastStatus(componentsRatio);
    }

    private string GetContrastStatus(double ratio)
    {
        return ratio switch
        {
            < 4.5 => "Fail",
            < 7.0 => "AA",
            _ => "AAA"
        };
    }

    private string GetAccentContrastStatus(double ratio)
    {
        return ratio < 4.5 ? "Fail" : "Pass";
    }

    public double CalculateContrastRatio(Color c1, Color c2)
    {
        var l1 = GetRelativeLuminance(c1);
        var l2 = GetRelativeLuminance(c2);
        var bright = Math.Max(l1, l2);
        var dark = Math.Min(l1, l2);
        return (bright + 0.05) / (dark + 0.05);
    }

    public double GetRelativeLuminance(Color c)
    {
        double R = c.R / 255.0, G = c.G / 255.0, B = c.B / 255.0;
        R = R <= 0.03928 ? R / 12.92 : Math.Pow((R + 0.055) / 1.055, 2.4);
        G = G <= 0.03928 ? G / 12.92 : Math.Pow((G + 0.055) / 1.055, 2.4);
        B = B <= 0.03928 ? B / 12.92 : Math.Pow((B + 0.055) / 1.055, 2.4);
        return 0.2126 * R + 0.7152 * G + 0.0722 * B;
    }

    private void EnsureContrastBindings()
    {
        if (string.IsNullOrWhiteSpace(_config.CurrentThemeContrastRatio))
            _config.CurrentThemeContrastRatio = "N/A";
        if (string.IsNullOrWhiteSpace(_config.CurrentThemeTextContrast))
            _config.CurrentThemeTextContrast = "N/A";
        if (string.IsNullOrWhiteSpace(_config.CurrentThemeHeadlinesContrast))
            _config.CurrentThemeHeadlinesContrast = "N/A";
        if (string.IsNullOrWhiteSpace(_config.CurrentThemeComponentsContrast))
            _config.CurrentThemeComponentsContrast = "N/A";
    }
}