using Avalonia.Data.Converters;

namespace EasyExtractCrossPlatform.Utilities;

public sealed class BooleanNegationConverter : IValueConverter
{
    public static readonly BooleanNegationConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolean)
            return !boolean;

        if (value is bool?)
        {
            var nullable = (bool?)value;
            if (nullable.HasValue)
                return !nullable.Value;
        }

        return AvaloniaProperty.UnsetValue;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Convert(value, targetType, parameter, culture);
    }
}