namespace EasyExtractCrossPlatform.Utilities;

public sealed class BooleanToBrushConverter : IValueConverter
{
    public IBrush? TrueBrush { get; set; }

    public IBrush? FalseBrush { get; set; }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool flag)
            return flag ? TrueBrush ?? Brushes.Transparent : FalseBrush ?? Brushes.Transparent;

        return FalseBrush ?? Brushes.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return AvaloniaProperty.UnsetValue;
    }
}