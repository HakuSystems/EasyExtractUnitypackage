using System.Globalization;
using System.Windows.Data;

namespace EasyExtract.Utilities;

public class FileSizeConverter : IValueConverter
{
    public object Convert(object? value, Type? targetType, object? parameter, CultureInfo culture)
    {
        if (value == null)
            return string.Empty;

        if (!long.TryParse(value.ToString(), out var bytes))
            return value;

        const long scale = 1024;
        var orders = new[] { "Bytes", "KB", "MB", "GB", "TB", "PB", "EB" };
        var order = 0;
        double len = bytes;

        while (len >= scale && order < orders.Length - 1)
        {
            order++;
            len /= scale;
        }

        return string.Format(culture, "{0:0.##} {1}", len, orders[order]);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException("FileSizeConverter is a one-way converter.");
    }
}