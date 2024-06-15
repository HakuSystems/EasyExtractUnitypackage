using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using EasyExtract.CustomDesign;
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
    public bool ExtractedCategoryStructure { get; set; } = true;

    public string DefaultTempPath { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EasyExtract", "Temp");

    public string LastExtractedPath { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EasyExtract", "Extracted");

    public int TotalExtracted { get; set; } = 0;
    public int TotalFilesExtracted { get; set; } = 0;

    public ObservableCollection<HistoryModel> History { get; set; } = new();
    public ObservableCollection<IgnoredUnitypackageModel> IgnoredUnitypackages { get; set; } = new();
    public BackgroundModel Backgrounds { get; set; } = new();
}