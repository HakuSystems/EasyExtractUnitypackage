using System.Collections.ObjectModel;
using EasyExtract.BetterExtraction;
using EasyExtract.Config.Models;

namespace EasyExtract.Config;

public class ConfigModel : INotifyPropertyChanged
{
    private string _accentColorHex = "#04d3be"; // Default Color from Colors.xaml
    private AvailableThemes _applicationTheme = AvailableThemes.System;
    private string _appTitle = "EasyExtractUnitypackage";
    private string _backgroundColorHex = "#2b2b2b"; // Default Color from Colors.xaml
    private bool _contextMenuToggle = true;
    private string _currentThemeComponentsContrast = "N/A";
    private string _currentThemeContrastRatio = "N/A";
    private string _currentThemeHeadlinesContrast = "N/A";
    private string _currentThemeTextContrast = "N/A";
    private BackgroundModel _custombackgroundImage = new();

    private string _defaultTempPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EasyExtract", "Temp");

    private bool _discordRpc = true;


    private DynamicScalingModes _dynamicScalingMode = DynamicScalingModes.Simple;
    private bool _extractedCategoryStructure = true;
    private ObservableCollection<ExtractedUnitypackageModel> _extractedUnitypackages = new();

    private bool _firstRun;


    private ObservableCollection<HistoryModel> _history = new();
    private ObservableCollection<IgnoredPackageInventory> _ignoredUnityPackages = new();

    private string _lastExtractedPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EasyExtract", "Extracted");

    private string _primaryColorHex = "#2ca7f2"; // Default Color from Colors.xaml
    private ObservableCollection<SearchEverythingModel> _searchEverything = new();
    private List<SearchEverythingModel> _searchEverythingResults = new();
    private string _secondaryColorHex = "#4D4D4D"; // Default Color from Colors.xaml
    private string _textColorHex = "#7fc5ff"; // Default Color from Colors.xaml

    private int _totalExtracted;
    private int _totalFilesExtracted;
    private List<UnitypackageFileInfo> _unitypackageFiles = new();
    private UpdateModel _update = new();
    private bool _uwUModeActive;

    public List<SearchEverythingModel> SearchEverythingResults
    {
        get => _searchEverythingResults;
        set
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
        set
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
        set
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
        set
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
        set
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
        set
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
        set
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
        set
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
        set
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
        set
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
        set
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
        set
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
        set
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

    public string LastExtractedPath
    {
        get => _lastExtractedPath;
        set
        {
            if (_lastExtractedPath != value)
            {
                _lastExtractedPath = value;
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
        set
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
        set
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
        set
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
        get => _custombackgroundImage;
        set
        {
            if (_custombackgroundImage != value)
            {
                _custombackgroundImage = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}