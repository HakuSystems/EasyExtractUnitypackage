using System;
using System.Collections.Generic;
using Avalonia.Controls;

namespace EasyExtractCrossPlatform.Models;

public class AppSettings
{
    public double SoundVolume { get; set; } = 1.0;
    public bool EnableSound { get; set; } = true;
    public string DefaultOutputPath { get; set; } = string.Empty;
    public long TotalSizeBytes { get; set; }
    public bool IsLoading { get; set; }
    public DateTimeOffset? LastExtractionTime { get; set; }
    public List<string> SearchEverythingResults { get; set; } = new();
    public List<UnityPackageFile> UnitypackageFiles { get; set; } = new();
    public string AppTitle { get; set; } = "EasyExtractUnitypackage";
    public string LicenseTier { get; set; } = "Free";
    public int ApplicationTheme { get; set; }
    public bool UwUModeActive { get; set; }
    public bool ContextMenuToggle { get; set; } = true;
    public bool FirstRun { get; set; } = true;
    public bool DiscordRpc { get; set; } = true;
    public UpdateSettings Update { get; set; } = new();
    public bool ExtractedCategoryStructure { get; set; } = true;
    public string DefaultTempPath { get; set; } = string.Empty;
    public int TotalExtracted { get; set; }
    public int CurrentExtractedCount { get; set; }
    public int TotalFilesToExtract { get; set; }
    public int TotalFilesExtracted { get; set; }
    public List<string> History { get; set; } = new();
    public List<string> IgnoredUnityPackages { get; set; } = new();
    public List<string> SearchEverything { get; set; } = new();
    public List<string> ExtractedUnitypackages { get; set; } = new();
    public CustomBackgroundImageSettings CustomBackgroundImage { get; set; } = new();
    public int TotalFolders { get; set; }
    public int TotalScripts { get; set; }
    public int TotalMaterials { get; set; }
    public int Total3DObjects { get; set; }
    public int TotalImages { get; set; }
    public int TotalAudios { get; set; }
    public int TotalControllers { get; set; }
    public int TotalConfigurations { get; set; }
    public int TotalEncryptedFiles { get; set; }
    public int TotalAnimations { get; set; }
    public bool EnableStackTrace { get; set; } = true;
    public bool EnablePerformanceLogging { get; set; } = true;
    public bool EnableMemoryTracking { get; set; } = true;
    public bool EnableAsyncLogging { get; set; } = true;
    public double? WindowWidth { get; set; }
    public double? WindowHeight { get; set; }
    public int? WindowPositionX { get; set; }
    public int? WindowPositionY { get; set; }
    public WindowState WindowState { get; set; } = WindowState.Normal;
}

public class UpdateSettings
{
    public string RepoName { get; set; } = "EasyExtractUnitypackage";
    public string RepoOwner { get; set; } = "HakuSystems";
    public bool AutoUpdate { get; set; } = true;
    public string CurrentVersion { get; set; } = "2.0.7.0";
}

public class UnityPackageFile
{
    public string FileName { get; set; } = string.Empty;
    public string FileHash { get; set; } = string.Empty;
    public string FileSize { get; set; } = string.Empty;
    public string FileDate { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string FileExtension { get; set; } = string.Empty;
    public bool IsInQueue { get; set; }
    public bool IsExtracting { get; set; }
}

public class CustomBackgroundImageSettings
{
    public string BackgroundPath { get; set; } =
        "https://raw.githubusercontent.com/HakuSystems/GraphicsStuff/main/EasyExtractUnitypackage_Background 8K.png";

    public bool IsEnabled { get; set; } = true;
    public double BackgroundOpacity { get; set; } = 0.2;
}