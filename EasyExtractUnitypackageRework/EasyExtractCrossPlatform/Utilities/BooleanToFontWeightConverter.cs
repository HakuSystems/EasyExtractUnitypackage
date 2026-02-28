namespace EasyExtractCrossPlatform.Utilities;

public sealed class BooleanToFontWeightConverter : IValueConverter
{
    public static readonly BooleanToFontWeightConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolean)
            return boolean ? FontWeight.SemiBold : FontWeight.Normal;

        if (value is bool?)
        {
            var nullable = (bool?)value;
            if (nullable.HasValue)
                return nullable.Value ? FontWeight.SemiBold : FontWeight.Normal;
        }

        return AvaloniaProperty.UnsetValue;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Convert(value, targetType, parameter, culture);
    }
}