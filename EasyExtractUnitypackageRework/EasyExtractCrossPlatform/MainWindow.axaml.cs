namespace EasyExtractCrossPlatform;

public partial class MainWindow : Window
{
    private const string UnityPackageExtension = ".unitypackage";
    private const string UnknownVersionLabel = "Version unknown";
    private const double CompactWidthBreakpoint = 1200;
    private const double CozyWidthBreakpoint = 1600;

    private static readonly HttpClient BackgroundHttpClient = new();
    private readonly Button? _batchExtractionButton;
    private readonly Button? _cancelExtractionButton;
    private readonly Button? _checkUpdatesButton;
    private readonly Button? _clearQueueButton;
    private readonly IBrush _defaultBackgroundBrush;
    private readonly Border? _dropZoneBorder;
    private readonly Grid? _dropZoneHostGrid;
    private readonly TextBlock? _dropZonePrimaryTextBlock;
    private readonly TextBlock? _dropZoneSecondaryTextBlock;
    private readonly IErrorDialogService _errorDialogService;
    private readonly EverythingSearchView? _everythingSearchView;
    private readonly Border? _extractionDashboard;
    private readonly TextBlock? _extractionDashboardAssetCount;
    private readonly TextBlock? _extractionDashboardAssetText;
    private readonly TextBlock? _extractionDashboardElapsed;
    private readonly TextBlock? _extractionDashboardNextPackage;
    private readonly TextBlock? _extractionDashboardOutputText;
    private readonly TextBlock? _extractionDashboardPackageText;
    private readonly ProgressBar? _extractionDashboardProgressBar;
    private readonly TextBlock? _extractionDashboardQueueCount;
    private readonly Border? _extractionDashboardSecurityBanner;
    private readonly TextBlock? _extractionDashboardSecurityText;
    private readonly TextBlock? _extractionDashboardSubtitle;
    private readonly DispatcherTimer _extractionElapsedTimer;
    private readonly TextBlock? _extractionOverviewAssetsExtractedText;
    private readonly TextBlock? _extractionOverviewLastExtractionText;
    private readonly TextBlock? _extractionOverviewPackagesCompletedText;
    private readonly IUnityPackageExtractionService _extractionService;
    private readonly Grid? _footerGrid;
    private readonly Grid? _heroGrid;
    private readonly Grid? _mainContentGrid;

    private readonly IMaliciousCodeDetectionService _maliciousCodeDetectionService;

    private readonly INotificationService _notificationService;

    private readonly Border? _overlayCard;
    private readonly ContentControl? _overlayContent;
    private readonly Border? _overlayHost;
    private readonly IUnityPackagePreviewService _previewService;
    private readonly Button? _processQueueButton;
    private readonly Control? _queueEmptyState;
    private readonly ObservableCollection<QueueItemDisplay> _queueItems = new();
    private readonly Dictionary<string, QueueItemDisplay> _queueItemsByPath = new(StringComparer.OrdinalIgnoreCase);
    private readonly ItemsControl? _queueItemsControl;
    private readonly ScrollViewer? _queueItemsScrollViewer;
    private readonly TextBlock? _queueSummaryTextBlock;
    private readonly Border? _searchHintContainer;
    private readonly Border? _searchIconBorder;
    private readonly Border? _searchResultsBorder;
    private readonly Border? _searchRevealHost;
    private readonly HashSet<string> _securityInfoNotified = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _securityScanGate = new();

    private readonly Dictionary<string, MaliciousCodeScanResult> _securityScanResults =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, Task<MaliciousCodeScanResult?>> _securityScanTasks =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly HashSet<string> _securityWarningsShown = new(StringComparer.OrdinalIgnoreCase);
    private readonly IAppServiceProvider _serviceProvider;
    private readonly string _standardDropPrimaryText = "Drag & drop files here";
    private readonly string _standardDropSecondaryText = "Supports batch extraction and live progress updates.";
    private readonly Button? _startExtractionButton;
    private readonly Grid? _startExtractionHeaderGrid;
    private readonly string[] _startupArguments;
    private readonly TextBox? _unityPackageSearchBox;
    private readonly IUpdateService _updateService;
    private readonly string _uwuDropPrimaryText;
    private readonly string _uwuDropSecondaryText;
    private readonly Border? _uwuModeBanner;
    private readonly TextBlock? _versionTextBlock;
    private Control? _activeOverlayContent;
    private UpdateManifest? _activeUpdateManifest;
    private object? _checkUpdatesButtonOriginalContent;
    private CreditsWindow? _creditsWindow;
    private Bitmap? _currentBackgroundBitmap;
    private string? _currentVersionDisplay;
    private string _defaultDropPrimaryText = "Drag & drop files here";
    private string _defaultDropSecondaryText = "Supports batch extraction and live progress updates.";
    private IDisposable? _dropStatusReset;
    private IDisposable? _dropSuccessReset;
    private IDisposable? _dropZoneVisibilityReset;
    private CancellationTokenSource? _extractionCts;
    private IDisposable? _extractionDashboardHideReset;
    private int _extractionOverviewAssetBaseline;
    private int _extractionOverviewCurrentPackageAssets;
    private int _extractionOverviewPackageBaseline;
    private int _extractionOverviewSessionAssets;
    private int _extractionOverviewSessionPackages;
    private DateTimeOffset? _extractionOverviewStartTime;
    private Stopwatch? _extractionStopwatch;
    private FeedbackWindow? _feedbackWindow;
    private HistoryWindow? _historyWindow;
    private bool _isCheckingForUpdates;
    private bool _isExtractionCancelling;
    private bool _isExtractionOverviewLive;
    private bool _isExtractionRunning;
    private bool _isSearchHover;
    private bool _isUpdateDownloadInProgress;
    private bool _lastDiscordPresenceEnabled;
    private PixelPoint? _lastNormalPosition;
    private Size? _lastNormalSize;
    private double? _lastUpdatePercentage;
    private UpdatePhase? _lastUpdatePhase;
    private DateTime _lastUpdateUiRefresh = DateTime.MinValue;
    private CancellationTokenSource? _overlayAnimationCts;
    private ScaleTransform? _overlayCardScaleTransform;
    private List<string>? _pendingStartupExtractions;
    private UpdateManifest? _pendingUpdateManifest;
    private IDisposable? _responsiveLayoutSubscription;
    private AppSettings _settings = new();
    private SettingsWindow? _settingsWindow;
    private IDisposable? _versionStatusReset;

    public MainWindow() : this(null)
    {
    }

    public MainWindow(string[]? startupArguments = null, IAppServiceProvider? serviceProvider = null)
    {
        _startupArguments = startupArguments ?? Array.Empty<string>();
        _serviceProvider = serviceProvider ?? AppServiceLocator.Current;
        _extractionService = _serviceProvider.GetRequiredService<IUnityPackageExtractionService>();
        _maliciousCodeDetectionService = _serviceProvider.GetRequiredService<IMaliciousCodeDetectionService>();
        _notificationService = _serviceProvider.GetRequiredService<INotificationService>();
        _errorDialogService = _serviceProvider.GetRequiredService<IErrorDialogService>();
        _previewService = _serviceProvider.GetRequiredService<IUnityPackagePreviewService>();
        _updateService = _serviceProvider.GetRequiredService<IUpdateService>();

        InitializeComponent();
        LinuxUiHelper.ApplyWindowTweaks(this);
        Classes.CollectionChanged += OnClassesChanged;
        _responsiveLayoutSubscription = ResponsiveWindowHelper.Enable(this);
        ApplyResponsiveLayouts();

        var localization = LocalizationManager.Instance;
        _uwuDropPrimaryText = localization.GetString("MainWindow_DragAmpDropFilesHereUwU");
        _uwuDropSecondaryText = localization.GetString("MainWindow_SupportsBatchExtractionAndLiveProgressUwU");

        _defaultBackgroundBrush = ResolveDefaultBackgroundBrush();
        _dropZoneBorder = this.FindControl<Border>("DropZoneBorder");
        _dropZoneHostGrid = this.FindControl<Grid>("DropZoneHostGrid");
        _dropZonePrimaryTextBlock = this.FindControl<TextBlock>("DropZonePrimaryTextBlock");
        _dropZoneSecondaryTextBlock = this.FindControl<TextBlock>("DropZoneSecondaryTextBlock");
        _heroGrid = this.FindControl<Grid>("HeroGrid");
        _mainContentGrid = this.FindControl<Grid>("MainContentGrid");
        _footerGrid = this.FindControl<Grid>("FooterGrid");
        if (_dropZonePrimaryTextBlock?.Text is { Length: > 0 } primaryText)
        {
            _defaultDropPrimaryText = primaryText;
            _standardDropPrimaryText = primaryText;
        }
        else
        {
            _standardDropPrimaryText = _defaultDropPrimaryText;
        }

        if (_dropZoneSecondaryTextBlock?.Text is { Length: > 0 } secondaryText)
        {
            _defaultDropSecondaryText = secondaryText;
            _standardDropSecondaryText = secondaryText;
        }
        else
        {
            _standardDropSecondaryText = _defaultDropSecondaryText;
        }

        _uwuModeBanner = this.FindControl<Border>("UwUModeBanner");
        _startExtractionHeaderGrid = this.FindControl<Grid>("StartExtractionHeaderGrid");
        _versionTextBlock = this.FindControl<TextBlock>("VersionTextBlock");
        _everythingSearchView = this.FindControl<EverythingSearchView>("EverythingSearch");
        _searchResultsBorder = this.FindControl<Border>("SearchResultsBorder");
        _searchRevealHost = this.FindControl<Border>("SearchRevealHost");
        _searchIconBorder = this.FindControl<Border>("SearchIconBorder");
        _searchHintContainer = this.FindControl<Border>("SearchHintContainer");
        _unityPackageSearchBox = this.FindControl<TextBox>("UnityPackageSearchBox");
        if (_everythingSearchView?.ViewModel is { } searchViewModel)
        {
            SearchViewModel = searchViewModel;
            SearchViewModel.PropertyChanged += OnSearchViewModelPropertyChanged;
            if (_unityPackageSearchBox is not null)
            {
                _unityPackageSearchBox.DataContext = SearchViewModel;
                _unityPackageSearchBox.KeyBindings.Clear();
                _unityPackageSearchBox.KeyBindings.Add(new KeyBinding
                {
                    Gesture = new KeyGesture(Key.Enter),
                    Command = SearchViewModel.SearchCommand
                });
                _unityPackageSearchBox.KeyBindings.Add(new KeyBinding
                {
                    Gesture = new KeyGesture(Key.Escape),
                    Command = SearchViewModel.ClearCommand
                });
            }
        }

        UpdateSearchUiState();
        ApplyResponsiveLayouts();

        _startExtractionButton = this.FindControl<Button>("StartExtractionButton");
        _cancelExtractionButton = this.FindControl<Button>("CancelExtractionButton");
        _batchExtractionButton = this.FindControl<Button>("BatchExtractionButton");
        _processQueueButton = this.FindControl<Button>("ProcessQueueButton");
        _extractionDashboard = this.FindControl<Border>("ExtractionDashboard");
        _extractionDashboardSubtitle = this.FindControl<TextBlock>("ExtractionDashboardSubtitle");
        _extractionDashboardQueueCount = this.FindControl<TextBlock>("ExtractionDashboardQueueCount");
        _extractionDashboardProgressBar = this.FindControl<ProgressBar>("ExtractionDashboardProgressBar");
        _extractionDashboardSecurityBanner = this.FindControl<Border>("ExtractionDashboardSecurityBanner");
        _extractionDashboardSecurityText = this.FindControl<TextBlock>("ExtractionDashboardSecurityText");
        _extractionDashboardPackageText = this.FindControl<TextBlock>("ExtractionDashboardPackageText");
        _extractionDashboardAssetText = this.FindControl<TextBlock>("ExtractionDashboardAssetText");
        _extractionDashboardOutputText = this.FindControl<TextBlock>("ExtractionDashboardOutputText");
        _extractionDashboardAssetCount = this.FindControl<TextBlock>("ExtractionDashboardAssetCount");
        _extractionDashboardElapsed = this.FindControl<TextBlock>("ExtractionDashboardElapsed");
        _extractionDashboardNextPackage = this.FindControl<TextBlock>("ExtractionDashboardNextPackage");
        _extractionOverviewPackagesCompletedText =
            this.FindControl<TextBlock>("ExtractionOverviewPackagesCompletedText");
        _extractionOverviewAssetsExtractedText =
            this.FindControl<TextBlock>("ExtractionOverviewAssetsExtractedText");
        _extractionOverviewLastExtractionText =
            this.FindControl<TextBlock>("ExtractionOverviewLastExtractionText");
        _checkUpdatesButton = this.FindControl<Button>("CheckUpdatesButton");
        if (_checkUpdatesButton is not null)
            _checkUpdatesButtonOriginalContent = _checkUpdatesButton.Content;
        _overlayHost = this.FindControl<Border>("OverlayHost");
        _overlayContent = this.FindControl<ContentControl>("OverlayContent");
        _overlayCard = this.FindControl<Border>("OverlayCard");
        if (_overlayCard?.RenderTransform is ScaleTransform transform)
        {
            _overlayCardScaleTransform = transform;
        }
        else if (_overlayCard is not null)
        {
            _overlayCardScaleTransform = new ScaleTransform(1, 1);
            _overlayCard.RenderTransform = _overlayCardScaleTransform;
        }

        _queueItemsControl = this.FindControl<ItemsControl>("QueueItemsControl");
        if (_queueItemsControl is not null)
            _queueItemsControl.ItemsSource = _queueItems;
        _queueEmptyState = this.FindControl<Control>("QueueEmptyState");
        _queueSummaryTextBlock = this.FindControl<TextBlock>("QueueSummaryTextBlock");
        _clearQueueButton = this.FindControl<Button>("ClearQueueButton");
        _queueItemsScrollViewer = this.FindControl<ScrollViewer>("QueueItemsScrollViewer");
        UpdateQueueVisualState();
        UpdateExtractionButtonsState();

        _extractionElapsedTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1),
            IsEnabled = false
        };
        _extractionElapsedTimer.Tick += OnExtractionElapsedTick;

        Closing += OnMainWindowClosing;
        PositionChanged += OnMainWindowPositionChanged;
        PropertyChanged += OnMainWindowPropertyChanged;
        Opened += OnMainWindowOpened;

        LoadSettings();
        SetVersionText();
        QueueDiscordPresenceUpdate("Dashboard");
        InitializeStartupExtractionTargets();
        QueueAutomaticUpdateCheck();
    }

    public EverythingSearchViewModel? SearchViewModel { get; }

    private void Button_OnClick(object? sender, RoutedEventArgs e)
    {
        LoggingService.LogError("TEST ERROR IGNORE");
    }
}