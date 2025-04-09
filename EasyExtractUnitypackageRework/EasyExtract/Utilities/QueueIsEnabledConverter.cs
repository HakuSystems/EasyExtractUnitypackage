using System.Globalization;
using System.Windows.Data;
using EasyExtract.Config;
using EasyExtract.Config.Models;

namespace EasyExtract.Utilities;

public class QueueIsEnabledConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not SearchEverythingModel model) return true;
        var inQueue = ConfigHandler.Instance.Config.UnitypackageFiles
            .Any(file => file.FileName!.Equals(model.FileName, StringComparison.InvariantCultureIgnoreCase));
        return !inQueue; // Return false if item is in queue (disable selection)
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}