using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace EasyExtract.Utilities;

[ValueConversion(typeof(double), typeof(double))]
public class RatioConverter : MarkupExtension, IValueConverter
{
    private static RatioConverter _instance;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || parameter is null)
            return null;
        if (value is double v && parameter is double p)
            return v * p;
        throw new ArgumentException("Both value and parameter must be of type double.");
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        return _instance ?? (_instance = new RatioConverter());
    }
}