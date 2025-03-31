using System.Globalization;
using System.Windows.Data;

namespace EasyExtract.Utilities;

public class VolumeToPercentageConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is float volume)
            return $"{(int)(volume * 100)}%";

        return "0%";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string percentageStr && percentageStr.EndsWith("%") &&
            float.TryParse(percentageStr.TrimEnd('%'), out var percentage))
            return percentage / 100;

        return 0f;
    }
}