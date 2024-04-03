using System.IO;
using System.Windows;

namespace EasyExtract.Config;

public class ConfigModel
{
    public string AppTitle { get; set; } = "EasyExtractUnitypackage";
    public string AppVersion { get; set; } = $"{Application.ResourceAssembly.GetName().Version}";
    public bool UwUModeActive { get; set; } = false;
    public bool IsFirstRun { get; set; } = true;
    public bool DiscordRpc { get; set; } = true;
    public bool AutoUpdate { get; set; } = true;
    public bool WindowsNotification { get; set; } = true;

    public static string DefaultTempPath { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EasyExtract", "Temp");

    public static string LastExtractedPath { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EasyExtract", "Extracted");
}