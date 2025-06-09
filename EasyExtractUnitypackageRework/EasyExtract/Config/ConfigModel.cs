using System.Collections.ObjectModel;
using EasyExtract.BetterExtraction;
using EasyExtract.Config.Models;

namespace EasyExtract.Config;

public sealed class ConfigModel : INotifyPropertyChanged
{
    private readonly string _accentColorHex = "#04d3be"; // Default Color from Colors.xaml
    private readonly string _appTitle = "EasyExtractUnitypackage";
    private readonly string _backgroundColorHex = "#2b2b2b"; // Default Color from Colors.xaml
    private readonly string _currentThemeComponentsContrast = "N/A";
    private readonly string _currentThemeContrastRatio = "N/A";
    private readonly string _currentThemeHeadlinesContrast = "N/A";
    private readonly string _currentThemeTextContrast = "N/A";
    private readonly BackgroundModel _customBackgroundImage = new();
    private readonly bool _extractedCategoryStructure = true;

    private readonly ObservableCollection<HistoryModel> _history = new();
    private readonly ObservableCollection<IgnoredPackageInventory> _ignoredUnityPackages = new();

    private readonly bool _isLoading;


    private readonly string _primaryColorHex = "#2ca7f2"; // Default Color from Colors.xaml
    private readonly ObservableCollection<SearchEverythingModel> _searchEverything = new();
    private readonly List<SearchEverythingModel> _searchEverythingResults = new();
    private readonly string _secondaryColorHex = "#4D4D4D"; // Default Color from Colors.xaml
    private readonly string _textColorHex = "#7fc5ff"; // Default Color from Colors.xaml

    private readonly int _totalEncryptedFiles;
    private readonly UpdateModel _update = new();
    private AvailableThemes _applicationTheme = AvailableThemes.System;
    private bool _contextMenuToggle = true;

    private int _currentExtractedCount;

    private string _defaultOutputPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EasyExtract", "Extracted");

    private string _defaultTempPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EasyExtract", "Temp");

    private bool _discordRpc = true;


    private DynamicScalingModes _dynamicScalingMode = DynamicScalingModes.Simple;
    private bool _enableAsyncLogging = true;
    private bool _enableMemoryTracking = true;
    private bool _enablePerformanceLogging = true;
    private bool _enableSound = true;

    // Logger configuration fields
    private bool _enableStackTrace = true;
    private ObservableCollection<ExtractedUnitypackageModel> _extractedUnitypackages = new();

    private bool _firstRun;

    private DateTime _lastExtractionTime;
    private float _soundVolume = 1f;

    private int _total3DObjects;

    private int _totalAnimations;

    private int _totalAudios;

    private int _totalConfigurations;

    private int _totalControllers;

    private int _totalExtracted;
    private int _totalFilesExtracted;

    private int _totalFilesToExtract;

    private int _totalFolders;

    private int _totalImages;

    private int _totalMaterials;

    private int _totalScripts;


    private long _totalSizeBytes;
    private List<UnitypackageFileInfo> _unitypackageFiles = new();
    private bool _uwUModeActive;
    private double _windowHeight = 650;
    private double _windowLeft = 100;
    private string _windowState = "Normal";

    private double _windowTop = 100;
    private double _windowWidth = 1000;

    public float SoundVolume
    {
        get => _soundVolume;
        set
        {
            if (Math.Abs(_soundVolume - value) > 0.01f)
            {
                _soundVolume = value;
                OnPropertyChanged();
            }
        }
    }

    public bool EnableSound
    {
        get => _enableSound;
        set
        {
            if (_enableSound != value)
            {
                _enableSound = value;
                OnPropertyChanged();
            }
        }
    }

    public string DefaultOutputPath
    {
        get => _defaultOutputPath;
        set
        {
            if (_defaultOutputPath != value)
            {
                _defaultOutputPath = value;
                OnPropertyChanged();
            }
        }
    }

    public double WindowTop
    {
        get => _windowTop;
        set
        {
            if (Math.Abs(_windowTop - value) > 0.01)
            {
                _windowTop = value;
                OnPropertyChanged();
            }
        }
    }

    public double WindowLeft
    {
        get => _windowLeft;
        set
        {
            if (Math.Abs(_windowLeft - value) > 0.01)
            {
                _windowLeft = value;
                OnPropertyChanged();
            }
        }
    }

    public double WindowWidth
    {
        get => _windowWidth;
        set
        {
            if (Math.Abs(_windowWidth - value) > 0.01)
            {
                _windowWidth = value;
                OnPropertyChanged();
            }
        }
    }

    public double WindowHeight
    {
        get => _windowHeight;
        set
        {
            if (Math.Abs(_windowHeight - value) > 0.01)
            {
                _windowHeight = value;
                OnPropertyChanged();
            }
        }
    }

    public string WindowState
    {
        get => _windowState;
        set
        {
            if (_windowState != value)
            {
                _windowState = value;
                OnPropertyChanged();
            }
        }
    }


    public long TotalSizeBytes
    {
        get => _totalSizeBytes;
        set
        {
            if (_totalSizeBytes != value)
            {
                _totalSizeBytes = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        init
        {
            if (_isLoading != value)
            {
                _isLoading = value;
                OnPropertyChanged();
            }
        }
    }

    public DateTime LastExtractionTime
    {
        get => _lastExtractionTime;
        set
        {
            if (_lastExtractionTime != value)
            {
                _lastExtractionTime = value;
                OnPropertyChanged();
            }
        }
    }


    public List<SearchEverythingModel> SearchEverythingResults
    {
        get => _searchEverythingResults;
        init
        {
            if (_searchEverythingResults != value)
            {
                _searchEverythingResults = value;
                OnPropertyChanged();
            }
        }
    }

    public List<UnitypackageFileInfo> UnitypackageFiles
    {
        get => _unitypackageFiles;
        set
        {
            if (_unitypackageFiles != value)
            {
                _unitypackageFiles = value;
                OnPropertyChanged();
            }
        }
    }


    public string TextColorHex
    {
        get => _textColorHex;
        init
        {
            if (_textColorHex != value)
            {
                _textColorHex = value;
                OnPropertyChanged();
            }
        }
    }

    public string BackgroundColorHex
    {
        get => _backgroundColorHex;
        init
        {
            if (_backgroundColorHex != value)
            {
                _backgroundColorHex = value;
                OnPropertyChanged();
            }
        }
    }

    public string PrimaryColorHex
    {
        get => _primaryColorHex;
        init
        {
            if (_primaryColorHex != value)
            {
                _primaryColorHex = value;
                OnPropertyChanged();
            }
        }
    }

    public string SecondaryColorHex
    {
        get => _secondaryColorHex;
        init
        {
            if (_secondaryColorHex != value)
            {
                _secondaryColorHex = value;
                OnPropertyChanged();
            }
        }
    }

    public string AccentColorHex
    {
        get => _accentColorHex;
        init
        {
            if (_accentColorHex != value)
            {
                _accentColorHex = value;
                OnPropertyChanged();
            }
        }
    }

    public string CurrentThemeContrastRatio
    {
        get => _currentThemeContrastRatio;
        init
        {
            if (_currentThemeContrastRatio != value)
            {
                _currentThemeContrastRatio = value;
                OnPropertyChanged();
            }
        }
    }

    public string CurrentThemeTextContrast
    {
        get => _currentThemeTextContrast;
        init
        {
            if (_currentThemeTextContrast != value)
            {
                _currentThemeTextContrast = value;
                OnPropertyChanged();
            }
        }
    }

    public string CurrentThemeHeadlinesContrast
    {
        get => _currentThemeHeadlinesContrast;
        init
        {
            if (_currentThemeHeadlinesContrast != value)
            {
                _currentThemeHeadlinesContrast = value;
                OnPropertyChanged();
            }
        }
    }

    public string CurrentThemeComponentsContrast
    {
        get => _currentThemeComponentsContrast;
        init
        {
            if (_currentThemeComponentsContrast != value)
            {
                _currentThemeComponentsContrast = value;
                OnPropertyChanged();
            }
        }
    }


    public DynamicScalingModes DynamicScalingMode
    {
        get => _dynamicScalingMode;
        set
        {
            if (_dynamicScalingMode != value)
            {
                _dynamicScalingMode = value;
                OnPropertyChanged();
            }
        }
    }

    public string AppTitle
    {
        get => _appTitle;
        init
        {
            if (_appTitle != value)
            {
                _appTitle = value;
                OnPropertyChanged();
            }
        }
    }

    public AvailableThemes ApplicationTheme
    {
        get => _applicationTheme;
        set
        {
            if (_applicationTheme != value)
            {
                _applicationTheme = value;
                OnPropertyChanged();
            }
        }
    }

    public bool UwUModeActive
    {
        get => _uwUModeActive;
        set
        {
            if (_uwUModeActive != value)
            {
                _uwUModeActive = value;
                OnPropertyChanged();
            }
        }
    }

    public bool ContextMenuToggle
    {
        get => _contextMenuToggle;
        set
        {
            if (_contextMenuToggle != value)
            {
                _contextMenuToggle = value;
                OnPropertyChanged();
            }
        }
    }


    public bool FirstRun
    {
        get => _firstRun;
        set
        {
            if (_firstRun != value)
            {
                _firstRun = value;
                OnPropertyChanged();
            }
        }
    }

    public bool DiscordRpc
    {
        get => _discordRpc;
        set
        {
            if (_discordRpc != value)
            {
                _discordRpc = value;
                OnPropertyChanged();
            }
        }
    }

    public UpdateModel Update
    {
        get => _update;
        init
        {
            if (_update != value)
            {
                _update = value;
                OnPropertyChanged();
            }
        }
    }

    public bool ExtractedCategoryStructure
    {
        get => _extractedCategoryStructure;
        init
        {
            if (_extractedCategoryStructure != value)
            {
                _extractedCategoryStructure = value;
                OnPropertyChanged();
            }
        }
    }


    public string DefaultTempPath
    {
        get => _defaultTempPath;
        set
        {
            if (_defaultTempPath != value)
            {
                _defaultTempPath = value;
                OnPropertyChanged();
            }
        }
    }


    public int TotalExtracted
    {
        get => _totalExtracted;
        set
        {
            if (_totalExtracted != value)
            {
                _totalExtracted = value;
                OnPropertyChanged();
            }
        }
    }

    public int CurrentExtractedCount
    {
        get => _currentExtractedCount;
        set
        {
            _currentExtractedCount = value;
            OnPropertyChanged();
        }
    }

    public int TotalFilesToExtract
    {
        get => _totalFilesToExtract;
        set
        {
            _totalFilesToExtract = value;
            OnPropertyChanged();
        }
    }

    public int TotalFilesExtracted
    {
        get => _totalFilesExtracted;
        set
        {
            if (_totalFilesExtracted != value)
            {
                _totalFilesExtracted = value;
                OnPropertyChanged();
            }
        }
    }

    public ObservableCollection<HistoryModel> History
    {
        get => _history;
        init
        {
            if (_history != value)
            {
                _history = value;
                OnPropertyChanged();
            }
        }
    }

    public ObservableCollection<IgnoredPackageInventory> IgnoredUnityPackages
    {
        get => _ignoredUnityPackages;
        init
        {
            if (_ignoredUnityPackages != value)
            {
                _ignoredUnityPackages = value;
                OnPropertyChanged();
            }
        }
    }

    public ObservableCollection<SearchEverythingModel> SearchEverything
    {
        get => _searchEverything;
        init
        {
            if (_searchEverything != value)
            {
                _searchEverything = value;
                OnPropertyChanged();
            }
        }
    }

    public ObservableCollection<ExtractedUnitypackageModel> ExtractedUnitypackages
    {
        get => _extractedUnitypackages;
        set
        {
            if (_extractedUnitypackages != value)
            {
                _extractedUnitypackages = value;
                OnPropertyChanged();
            }
        }
    }

    public BackgroundModel CustomBackgroundImage
    {
        get => _customBackgroundImage;
        init
        {
            if (_customBackgroundImage != value)
            {
                _customBackgroundImage = value;
                OnPropertyChanged();
            }
        }
    }

    public int TotalFolders
    {
        get => _totalFolders;
        set
        {
            if (_totalFolders != value)
            {
                _totalFolders = value;
                OnPropertyChanged();
            }
        }
    }

    public int TotalScripts
    {
        get => _totalScripts;
        set
        {
            if (_totalScripts != value)
            {
                _totalScripts = value;
                OnPropertyChanged();
            }
        }
    }

    public int TotalMaterials
    {
        get => _totalMaterials;
        set
        {
            if (_totalMaterials != value)
            {
                _totalMaterials = value;
                OnPropertyChanged();
            }
        }
    }

    public int Total3DObjects
    {
        get => _total3DObjects;
        set
        {
            if (_total3DObjects != value)
            {
                _total3DObjects = value;
                OnPropertyChanged();
            }
        }
    }

    public int TotalImages
    {
        get => _totalImages;
        set
        {
            if (_totalImages != value)
            {
                _totalImages = value;
                OnPropertyChanged();
            }
        }
    }

    public int TotalAudios
    {
        get => _totalAudios;
        set
        {
            if (_totalAudios != value)
            {
                _totalAudios = value;
                OnPropertyChanged();
            }
        }
    }

    public int TotalControllers
    {
        get => _totalControllers;
        set
        {
            if (_totalControllers != value)
            {
                _totalControllers = value;
                OnPropertyChanged();
            }
        }
    }

    public int TotalConfigurations
    {
        get => _totalConfigurations;
        set
        {
            if (_totalConfigurations != value)
            {
                _totalConfigurations = value;
                OnPropertyChanged();
            }
        }
    }

    public int TotalEncryptedFiles
    {
        get => _totalEncryptedFiles;
        init
        {
            if (_totalEncryptedFiles != value)
            {
                _totalEncryptedFiles = value;
                OnPropertyChanged();
            }
        }
    }

    public int TotalAnimations
    {
        get => _totalAnimations;
        set
        {
            if (_totalAnimations != value)
            {
                _totalAnimations = value;
                OnPropertyChanged();
            }
        }
    }

    // Logger configuration properties
    public bool EnableStackTrace
    {
        get => _enableStackTrace;
        set
        {
            if (_enableStackTrace != value)
            {
                _enableStackTrace = value;
                OnPropertyChanged();
            }
        }
    }

    public bool EnablePerformanceLogging
    {
        get => _enablePerformanceLogging;
        set
        {
            if (_enablePerformanceLogging != value)
            {
                _enablePerformanceLogging = value;
                OnPropertyChanged();
            }
        }
    }

    public bool EnableMemoryTracking
    {
        get => _enableMemoryTracking;
        set
        {
            if (_enableMemoryTracking != value)
            {
                _enableMemoryTracking = value;
                OnPropertyChanged();
            }
        }
    }

    public bool EnableAsyncLogging
    {
        get => _enableAsyncLogging;
        set
        {
            if (_enableAsyncLogging != value)
            {
                _enableAsyncLogging = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}