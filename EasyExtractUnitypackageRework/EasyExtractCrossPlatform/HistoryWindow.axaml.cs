namespace EasyExtractCrossPlatform;

public partial class HistoryWindow : Window
{
    private readonly Border? _primarySectionBorder;
    private readonly IDisposable? _responsiveLayoutSubscription;
    private readonly Border? _secondarySectionBorder;
    private readonly Grid? _splitGrid;
    private readonly IDisposable _windowPlacementTracker;

    public HistoryWindow()
    {
        InitializeComponent();
        LinuxUiHelper.ApplyWindowTweaks(this);
        _responsiveLayoutSubscription = ResponsiveWindowHelper.Enable(this);
        _windowPlacementTracker = WindowPlacementService.Attach(this, nameof(HistoryWindow));
        _splitGrid = this.FindControl<Grid>("SplitGrid");
        _primarySectionBorder = this.FindControl<Border>("PrimarySectionBorder");
        _secondarySectionBorder = this.FindControl<Border>("SecondarySectionBorder");
        Classes.CollectionChanged += OnClassesChanged;
        ApplyResponsiveLayout();
    }

    public HistoryWindow(HistoryViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        Classes.CollectionChanged -= OnClassesChanged;
        _responsiveLayoutSubscription?.Dispose();
        _windowPlacementTracker.Dispose();
    }

    private void OnClassesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ApplyResponsiveLayout();
    }

    private void ApplyResponsiveLayout()
    {
        if (_splitGrid is null || _primarySectionBorder is null || _secondarySectionBorder is null)
            return;

        if (Classes.Contains("compact"))
        {
            _splitGrid.ColumnDefinitions = new ColumnDefinitions("*");
            _splitGrid.RowDefinitions = new RowDefinitions("Auto,Auto");
            Grid.SetColumn(_primarySectionBorder, 0);
            Grid.SetRow(_primarySectionBorder, 0);
            Grid.SetColumn(_secondarySectionBorder, 0);
            Grid.SetRow(_secondarySectionBorder, 1);
        }
        else
        {
            _splitGrid.ColumnDefinitions = new ColumnDefinitions("2*,*");
            _splitGrid.RowDefinitions = new RowDefinitions("Auto");
            Grid.SetColumn(_primarySectionBorder, 0);
            Grid.SetRow(_primarySectionBorder, 0);
            Grid.SetColumn(_secondarySectionBorder, 1);
            Grid.SetRow(_secondarySectionBorder, 0);
        }
    }
}