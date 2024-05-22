using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using Wpf.Ui.Controls;

namespace EasyExtract.Config;

public class ExtractedUnitypackageModel : INotifyPropertyChanged
{
    private InfoBarSeverity detailsSeverity;
    private List<ExtractedFiles> subdirectoryItems = new();
    private string unitypackageDetails;
    private bool isChecked;
    private DateTime unitypackageExtractedDate;


    private string unitypackageName;
    private string unitypackagePath;
    private string unitypackageSize;
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

    private string unitypackageTotalFileCountMessage =>
        $"Total Files: {UnitypackageTotalFileCount:N2} / Package Size: {UnitypackageSize}";


    public string UnitypackageTotalFileCountMessage => unitypackageTotalFileCountMessage;

    public bool IsChecked
    {
        get => isChecked;
        set
        {
            isChecked = value;
            OnPropertyChanged();
        }
    }
    public string UnitypackageDetails
    {
        get => unitypackageDetails;
        set
        {
            unitypackageDetails = value;
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

    public int UnitypackageTotalFileCount
    {
        get => unitypackageTotalFileCount;
        set
        {
            unitypackageTotalFileCount = value;
            OnPropertyChanged();
        }
    }

    public int UnitypackageTotalFolderCount
    {
        get => unitypackageTotalFolderCount;
        set
        {
            unitypackageTotalFolderCount = value;
            OnPropertyChanged();
        }
    }

    public int UnitypackageTotalScriptCount
    {
        get => unitypackageTotalScriptCount;
        set
        {
            unitypackageTotalScriptCount = value;
            OnPropertyChanged();
        }
    }

    public int UnitypackageTotalShaderCount
    {
        get => unitypackageTotalShaderCount;
        set
        {
            unitypackageTotalShaderCount = value;
            OnPropertyChanged();
        }
    }

    public int UnitypackageTotalPrefabCount
    {
        get => unitypackageTotalPrefabCount;
        set
        {
            unitypackageTotalPrefabCount = value;
            OnPropertyChanged();
        }
    }

    public int UnitypackageTotal3DObjectCount
    {
        get => unitypackageTotal3DObjectCount;
        set
        {
            unitypackageTotal3DObjectCount = value;
            OnPropertyChanged();
        }
    }

    public int UnitypackageTotalImageCount
    {
        get => unitypackageTotalImageCount;
        set
        {
            unitypackageTotalImageCount = value;
            OnPropertyChanged();
        }
    }

    public int UnitypackageTotalAudioCount
    {
        get => unitypackageTotalAudioCount;
        set
        {
            unitypackageTotalAudioCount = value;
            OnPropertyChanged();
        }
    }

    public int UnitypackageTotalAnimationCount
    {
        get => unitypackageTotalAnimationCount;
        set
        {
            unitypackageTotalAnimationCount = value;
            OnPropertyChanged();
        }
    }

    public int UnitypackageTotalSceneCount
    {
        get => unitypackageTotalSceneCount;
        set
        {
            unitypackageTotalSceneCount = value;
            OnPropertyChanged();
        }
    }

    public int UnitypackageTotalMaterialCount
    {
        get => unitypackageTotalMaterialCount;
        set
        {
            unitypackageTotalMaterialCount = value;
            OnPropertyChanged();
        }
    }

    public int UnitypackageTotalAssetCount
    {
        get => unitypackageTotalAssetCount;
        set
        {
            unitypackageTotalAssetCount = value;
            OnPropertyChanged();
        }
    }

    public int UnitypackageTotalControllerCount
    {
        get => unitypackageTotalControllerCount;
        set
        {
            unitypackageTotalControllerCount = value;
            OnPropertyChanged();
        }
    }

    public int UnitypackageTotalFontCount
    {
        get => unitypackageTotalFontCount;
        set
        {
            unitypackageTotalFontCount = value;
            OnPropertyChanged();
        }
    }

    public int UnitypackageTotalConfigurationCount
    {
        get => unitypackageTotalConfigurationCount;
        set
        {
            unitypackageTotalConfigurationCount = value;
            OnPropertyChanged();
        }
    }

    public int UnitypackageTotalDataCount
    {
        get => unitypackageTotalDataCount;
        set
        {
            unitypackageTotalDataCount = value;
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

    public string UnitypackageSize
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

    public List<ExtractedFiles> SubdirectoryItems
    {
        get => subdirectoryItems;
        set
        {
            subdirectoryItems = value;
            OnPropertyChanged();
        }
    }

    public Dictionary<string, List<ExtractedFiles>> SubdirectoryItemsGroupedByCategory => SubdirectoryItems
        .GroupBy(file => file.Category)
        .ToDictionary(group => group.Key, group => group.ToList());

    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class ExtractedFiles : INotifyPropertyChanged
{
    private string _category;
    private string _extension;
    private DateTime _extractedDate;
    private string _fileName;
    private string _filePath;
    private bool _isChecked;
    private ImageSource? _previewImage;
    private string _size;
    private string _symbolIcon;


    public ImageSource? PreviewImage
    {
        get => _previewImage;
        set
        {
            _previewImage = value;
            OnPropertyChanged();
        }
    }

    public string SymbolIconImage
    {
        get => _symbolIcon;
        set
        {
            _symbolIcon = value;
            OnPropertyChanged();
        }
    }

    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            _isChecked = value;
            OnPropertyChanged();
        }
    }

    public string FileName
    {
        get => _fileName;
        set
        {
            _fileName = value;
            OnPropertyChanged();
        }
    }

    public string FilePath
    {
        get => _filePath;
        set
        {
            _filePath = value;
            OnPropertyChanged();
        }
    }

    public string Category
    {
        get => _category;
        set
        {
            _category = value;
            OnPropertyChanged();
        }
    }

    public string Extension
    {
        get => _extension;
        set
        {
            _extension = value;
            OnPropertyChanged();
        }
    }

    public string Size
    {
        get => _size;
        set
        {
            _size = value;
            OnPropertyChanged();
        }
    }

    public DateTime ExtractedDate
    {
        get => _extractedDate;
        set
        {
            _extractedDate = value;
            OnPropertyChanged();
        }
    }

    private string unityFileMessasge =>
        $"Category: {Category} / File Size: {Size}";


    public string UnityFileMessasge => unityFileMessasge;

    private string unityFileMessasgeTooltip =>
        $"Category: {Category}\nFile Size: {Size}";


    public string UnityFileMessasgeTooltip => unityFileMessasgeTooltip;

    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}