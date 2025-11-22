using Avalonia.Data;
using Avalonia.Data.Converters;

namespace EasyExtractCrossPlatform.Utilities;

public sealed class StringIsNullOrWhiteSpaceConverter : IValueConverter
{
    public static StringIsNullOrWhiteSpaceConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is BindingNotification notification)
        {
            if (!notification.HasValue) return BindingOperations.DoNothing;

            value = notification.Value;
        }

        var text = value switch
        {
            string s => s,
            null => null,
            _ => value.ToString()
        };

        var isNullOrWhitespace = string.IsNullOrWhiteSpace(text);
        return ApplyParameter(isNullOrWhitespace, parameter);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException("StringIsNullOrWhiteSpaceConverter does not support ConvertBack.");
    }

    private static bool ApplyParameter(bool result, object? parameter)
    {
        if (parameter is string param &&
            param.Equals("Invert", StringComparison.OrdinalIgnoreCase))
            return !result;

        return result;
    }
}