using System.Collections.ObjectModel;
using System.Windows.Media;
using Wpf.Ui.Controls;

namespace EasyExtract.Config.Models;

public class ExtractedUnitypackageModel : INotifyPropertyChanged
{
    private bool _hasEncryptedDll;


    private bool _isCategoryView = true;
    private ObservableCollection<ExtractedFiles> _subdirectoryItems = new();
    private int base64DetectionCount;
    private InfoBarSeverity detailsSeverity = InfoBarSeverity.Informational;

    private int linkDetectionCount;
    private int malicousDiscordWebhookCount;

    private bool packageIsChecked;
    private List<ExtractedFiles> subdirectoryItems = new();
    private string unitypackageDetails = "No Details Available";
    private DateTime unitypackageExtractedDate = DateTime.Now;


    private string unitypackageName = "No Name Available";
    private string unitypackagePath = "No Path Available";
    private string? unitypackageSize = "No Size Available";
    private int unitypackageTotal3DObjectCount;
    private int unitypackageTotalAnimationCount;
    private int unitypackageTotalAssetCount;
    private int unitypackageTotalAudioCount;
    private int unitypackageTotalConfigurationCount;
    private int unitypackageTotalControllerCount;
    private int unitypackageTotalDataCount;

    private int unitypackageTotalFileCount;
    private int unitypackageTotalFolderCount;
    private int unitypackageTotalFontCount;
    private int unitypackageTotalImageCount;
    private int unitypackageTotalMaterialCount;
    private int unitypackageTotalPrefabCount;
    private int unitypackageTotalSceneCount;

    private int unitypackageTotalScriptCount;
    private int unitypackageTotalShaderCount;

    public int UnitypackageTotalFolderCount { get; set; }
    public int UnitypackageTotalFileCount { get; set; }
    public int UnitypackageTotalScriptCount { get; set; }
    public int UnitypackageTotalMaterialCount { get; set; }
    public int UnitypackageTotal3DObjectCount { get; set; }
    public int UnitypackageTotalImageCount { get; set; }
    public int UnitypackageTotalAudioCount { get; set; }
    public int UnitypackageTotalControllerCount { get; set; }
    public int UnitypackageTotalConfigurationCount { get; set; }
    public int UnitypackageTotalAnimationCount { get; set; }
    public int UnitypackageTotalAssetCount { get; set; }
    public int UnitypackageTotalSceneCount { get; set; }
    public int UnitypackageTotalShaderCount { get; set; }
    public int UnitypackageTotalPrefabCount { get; set; }
    public int UnitypackageTotalFontCount { get; set; }
    public int UnitypackageTotalDataCount { get; set; }


    public bool HasEncryptedDll
    {
        get => _hasEncryptedDll;
        set
        {
            _hasEncryptedDll = value;
            OnPropertyChanged();
        }
    }

    private string encryptedDllMessage => HasEncryptedDll ? "Encrypted DLL detected!" : "No encrypted DLLs";
    public string EncryptedDllMessage => encryptedDllMessage;

    public SolidColorBrush GetEncryptedDllColor =>
        HasEncryptedDll ? new SolidColorBrush(Colors.Red) : new SolidColorBrush(Colors.Green);

    public bool IsDangerousPackage => HasEncryptedDll || MalicousDiscordWebhookCount > 0 || LinkDetectionCount > 0;

    public string PackageSecuritySummary
    {
        get
        {
            var messages = new List<string>();
            if (HasEncryptedDll) messages.Add("Encrypted DLL detected!");
            if (MalicousDiscordWebhookCount > 0) messages.Add("Discord webhook detected!");
            if (LinkDetectionCount > 0) messages.Add("Suspicious links detected!");
            return messages.Any() ? $"⚠️ Possible Dangerous Package ({string.Join(", ", messages)})" : "";
        }
    }

    public bool IsCategoryView
    {
        get => _isCategoryView;
        set
        {
            _isCategoryView = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CurrentStructure));
        }
    }

    public object CurrentStructure => SubdirectoryItemsGroupedByCategory;

    public bool PackageIsChecked
    {
        get => packageIsChecked;
        set
        {
            packageIsChecked = value;
            OnPropertyChanged();
        }
    }

    public InfoBarSeverity DetailsSeverity
    {
        get => detailsSeverity;
        set
        {
            detailsSeverity = value;
            OnPropertyChanged();
        }
    }

    public int MalicousDiscordWebhookCount
    {
        get => malicousDiscordWebhookCount;
        set
        {
            malicousDiscordWebhookCount = value;
            OnPropertyChanged();
        }
    }

    public int LinkDetectionCount
    {
        get => linkDetectionCount;
        set
        {
            linkDetectionCount = value;
            OnPropertyChanged();
        }
    }

    public string UnitypackageName
    {
        get => unitypackageName;
        set
        {
            unitypackageName = value;
            OnPropertyChanged();
        }
    }

    public string UnitypackagePath
    {
        get => unitypackagePath;
        set
        {
            unitypackagePath = value;
            OnPropertyChanged();
        }
    }

    public string? UnitypackageSize
    {
        get => unitypackageSize;
        set
        {
            unitypackageSize = value;
            OnPropertyChanged();
        }
    }

    public DateTime UnitypackageExtractedDate
    {
        get => unitypackageExtractedDate;
        set
        {
            unitypackageExtractedDate = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<ExtractedFiles> SubdirectoryItems
    {
        get => _subdirectoryItems;
        set
        {
            if (_subdirectoryItems != value)
            {
                _subdirectoryItems = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SubdirectoryItemsGroupedByCategory));
                OnPropertyChanged(nameof(CurrentStructure));
            }
        }
    }

    // Grouping logic stays unchanged
    public Dictionary<string, List<ExtractedFiles>> SubdirectoryItemsGroupedByCategory =>
        SubdirectoryItems
            .GroupBy(file => file.Category)
            .ToDictionary(group => group.Key, group => group.ToList());


    public event PropertyChangedEventHandler? PropertyChanged;

    public virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}