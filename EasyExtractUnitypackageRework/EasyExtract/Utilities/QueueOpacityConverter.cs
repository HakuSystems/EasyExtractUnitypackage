using System.Globalization;
using System.Windows.Data;
using EasyExtract.Config;
using EasyExtract.Config.Models;

namespace EasyExtract.Utilities;

public class QueueOpacityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is SearchEverythingModel model)
        {
            var inQueue = ConfigHandler.Instance.Config.UnitypackageFiles.Any(file =>
                file.FileName!.Equals(model.FileName, StringComparison.InvariantCultureIgnoreCase));
            return inQueue ? 0.5 : 1.0;
        }

        return 1.0;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}