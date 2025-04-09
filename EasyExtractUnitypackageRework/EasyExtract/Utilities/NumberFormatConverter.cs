using System.Globalization;
using System.Windows.Data;

namespace EasyExtract.Utilities;

public class NumberFormatConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo? culture)
    {
        if (targetType != typeof(string))
            return DependencyProperty.UnsetValue;

        if (value == null)
            return string.Empty;

        double number;
        if (value is double d)
            number = d;
        else
            try
            {
                number = System.Convert.ToDouble(value, culture);
            }
            catch
            {
                return DependencyProperty.UnsetValue;
            }

        var nfi = culture != null
            ? (NumberFormatInfo)culture.NumberFormat.Clone()
            : (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();

        nfi.NumberGroupSeparator = ".";
        nfi.NumberDecimalDigits = 0;

        return number.ToString("#,0", nfi);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}