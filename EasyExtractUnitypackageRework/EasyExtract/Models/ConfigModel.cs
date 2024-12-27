using System.Collections.ObjectModel;

namespace EasyExtract.Models;

public class ConfigModel
{
    public DynamicScalingModes DynamicScalingMode { get; set; } = DynamicScalingModes.Simple;
    public string AppTitle { get; set; } = "EasyExtractUnitypackage";
    public AvailableThemes ApplicationTheme { get; set; } = AvailableThemes.System;
    public bool EasterEggHeader { get; set; } = true;
    public bool UwUModeActive { get; set; }
    public bool ContextMenuToggle { get; set; } = true;
    public bool IntroLogoAnimation { get; set; }
    public RunsModel Runs { get; set; } = new();
    public bool DiscordRpc { get; set; } = true;
    public UpdateModel Update { get; set; } = new();
    public bool ExtractedCategoryStructure { get; set; } = true;
    public bool BorderThicknessActive { get; set; }

    public string DefaultTempPath { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EasyExtract", "Temp");

    public string LastExtractedPath { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EasyExtract", "Extracted");

    public int TotalExtracted { get; set; }
    public int TotalFilesExtracted { get; set; }

    public ObservableCollection<HistoryModel> History { get; set; } = new();
    public ObservableCollection<IgnoredPackageInventory> IgnoredUnityPackages { get; set; } = new();
    public ObservableCollection<SearchEverythingModel> SearchEverything { get; set; } = new();
    public ObservableCollection<ExtractedUnitypackageModel> ExtractedUnitypackages { get; set; } = new();
    public BackgroundModel Backgrounds { get; set; } = new();
}