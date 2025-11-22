namespace EasyExtractCrossPlatform;

public partial class CreditsWindow : Window
{
    private readonly Border? _detailPanelBorder;
    private readonly Grid? _detailsGrid;
    private readonly Border? _listPanelBorder;
    private readonly IDisposable? _responsiveLayoutSubscription;
    private readonly IDisposable _windowPlacementTracker;

    public CreditsWindow()
    {
        InitializeComponent();
        LinuxUiHelper.ApplyWindowTweaks(this);
        _responsiveLayoutSubscription = ResponsiveWindowHelper.Enable(this);
        _windowPlacementTracker = WindowPlacementService.Attach(this, nameof(CreditsWindow));
        _detailsGrid = this.FindControl<Grid>("DetailsGrid");
        _listPanelBorder = this.FindControl<Border>("ListPanelBorder");
        _detailPanelBorder = this.FindControl<Border>("DetailPanelBorder");
        Classes.CollectionChanged += OnClassesChanged;
        ApplyResponsiveLayout();
        ViewModel = new CreditsViewModel();
        DataContext = ViewModel;
    }

    public CreditsViewModel ViewModel { get; }

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
        if (_detailsGrid is null || _listPanelBorder is null || _detailPanelBorder is null)
            return;

        if (Classes.Contains("compact"))
        {
            _detailsGrid.ColumnDefinitions = new ColumnDefinitions("*");
            _detailsGrid.RowDefinitions = new RowDefinitions("Auto,Auto");
            Grid.SetColumn(_listPanelBorder, 0);
            Grid.SetRow(_listPanelBorder, 0);
            Grid.SetColumn(_detailPanelBorder, 0);
            Grid.SetRow(_detailPanelBorder, 1);
        }
        else
        {
            _detailsGrid.ColumnDefinitions = new ColumnDefinitions("320,*");
            _detailsGrid.RowDefinitions = new RowDefinitions("Auto");
            Grid.SetColumn(_listPanelBorder, 0);
            Grid.SetRow(_listPanelBorder, 0);
            Grid.SetColumn(_detailPanelBorder, 1);
            Grid.SetRow(_detailPanelBorder, 0);
        }
    }
}