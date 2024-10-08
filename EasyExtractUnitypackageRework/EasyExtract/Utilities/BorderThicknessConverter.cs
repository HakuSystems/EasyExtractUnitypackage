using System.Globalization;
using System.Windows.Data;

namespace EasyExtract.Utilities;

public class BorderThicknessConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isActive)
            // Return a Thickness of 1 if active, else 0
            return isActive ? new Thickness(0.1) : new Thickness(0);
        return new Thickness(0); // Default to no border
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // Implement if necessary
        return Binding.DoNothing;
    }
}