using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace EasyExtract.Utilities;

public class BackgroundAndOpacityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values[0] is ImageBrush imageBrush && values[1] is double opacity)
        {
            var newBrush = new ImageBrush(imageBrush.ImageSource)
            {
                Opacity = opacity,
                Stretch = Stretch.UniformToFill
            };
            return newBrush;
        }

        return Binding.DoNothing;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        if (value is ImageBrush imageBrush)
            return
            [
                imageBrush, imageBrush.Opacity
            ];

        return
        [
            Binding.DoNothing, Binding.DoNothing
        ];
    }
}