using System.IO;
using System.Windows.Controls;
using EasyExtract.UserControls;

namespace EasyExtract.Config;

public class ConfigModel
{
    public bool UwUModeActive { get; set; } = false;
    public bool IsFirstRun { get; set; } = true;
    public bool DiscordRpc { get; set; } = true;
    public bool AutoUpdate { get; set; } = true;
    public bool WindowsNotification { get; set; } = true;
    public string DefaultTempPath { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EasyExtract", "Temp");
    public string LastExtractedPath { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EasyExtract", "Extracted");
}