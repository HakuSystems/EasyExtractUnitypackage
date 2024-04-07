using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using Wpf.Ui.Appearance;

namespace EasyExtract.Config;

public class ConfigModel
{
    public string AppTitle { get; set; } = "EasyExtractUnitypackage";
    public string CurrentVersion { get; set; } = $"V{Assembly.GetExecutingAssembly().GetName().Version}";
    public ApplicationTheme ApplicationTheme { get; set; } = ApplicationTheme.Dark;
    public bool UwUModeActive { get; set; } = false;
    public bool IsFirstRun { get; set; } = true;
    public bool DiscordRpc { get; set; } = true;
    public bool AutoUpdate { get; set; } = true;
    public bool WindowsNotification { get; set; } = true;

    public static string DefaultTempPath { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EasyExtract", "Temp");

    public static string LastExtractedPath { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EasyExtract", "Extracted");

    public int TotalExtracted { get; set; } = 0;
    public int TotalFilesExtracted { get; set; } = 0;

    public ObservableCollection<HistoryModel> History { get; set; }
}