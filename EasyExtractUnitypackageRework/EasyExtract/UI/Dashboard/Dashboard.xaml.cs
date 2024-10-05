using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using EasyExtract.Config;
using EasyExtract.CustomDesign;
using EasyExtract.Updater;
using EasyExtract.UserControls;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using TextBlock = Wpf.Ui.Controls.TextBlock;

namespace EasyExtract;

public partial class Dashboard : FluentWindow
{
    private static UserControl ContentFrame;
    private static Dashboard instance;
    private readonly BackgroundManager _backgroundManager = BackgroundManager.Instance;
    private readonly BetterLogger _logger = new();
    private readonly UpdateHandler _updateHandler = new();
    private readonly ConfigHelper ConfigHelper = new();

    public Dashboard()
    {
        InitializeComponent();
        DataContext = this;
        ContentFrame = new UserControls.Extraction();
    }

    public static Dashboard Instance
    {
        get => instance ??= new Dashboard();
    }


    private void HeartIcon_OnMouseEnter(object sender, MouseEventArgs e)
    {
        HeartIcon.Symbol = SymbolRegular.HeartBroken24;
    }

    private void HeartIcon_OnMouseLeave(object sender, MouseEventArgs e)
    {
        HeartIcon.Symbol = SymbolRegular.Heart24;
    }

    private void HeartIcon_OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        HeartIcon.Symbol = SymbolRegular.HeartPulse24;
        NavView.Navigate(typeof(EasterEgg));
    }

    private async void Dashboard_OnLoaded(object sender, RoutedEventArgs e)
    {
        SystemThemeWatcher.Watch(this);
        await ConfigHelper.ReadConfigAsync();
        VersionTxt.Content = "V" + Application.ResourceAssembly.GetName().Version;
        _backgroundManager.UpdateBackground(ConfigHelper.Config.Backgrounds.BackgroundPath);
        _backgroundManager.UpdateOpacity(ConfigHelper.Config.Backgrounds.BackgroundOpacity);

        var theme = ConfigHelper.Config.ApplicationTheme;
        switch (theme)
        {
            case ApplicationTheme.Dark:
                ApplicationThemeManager.Apply(ApplicationTheme.Dark);
                await _logger.LogAsync("Applied Dark Theme", "Dashboard.xaml.cs", Importance.Info);
                break;
            case ApplicationTheme.Light:
                ApplicationThemeManager.Apply(ApplicationTheme.Light);
                await _logger.LogAsync("Applied Light Theme", "Dashboard.xaml.cs", Importance.Info);
                break;
            case ApplicationTheme.HighContrast:
                ApplicationThemeManager.Apply(ApplicationTheme.HighContrast);
                await _logger.LogAsync("Applied High Contrast Theme", "Dashboard.xaml.cs", Importance.Info);
                break;
            default:
                ApplicationThemeManager.Apply(ApplicationTheme.Dark);
                await _logger.LogAsync("Applied Dark Theme", "Dashboard.xaml.cs", Importance.Info)
                    .ConfigureAwait(false);
                break;
        }

        var isUpToDate = await _updateHandler.IsUpToDate();
        var updateAvailable = !isUpToDate;

        await Dispatcher.InvokeAsync(() =>
        {
            CheckForUpdatesTxt.Text = updateAvailable ? "New Update Available" : "Check for Updates";
            CheckForUpdatesTxt.Foreground =
                new SolidColorBrush(updateAvailable ? Color.FromRgb(255, 0, 0) : Color.FromRgb(0, 255, 0));
            CheckForUpdatesDesc.Text = updateAvailable
                ? "Click here to update EasyExtractUnitypackage!"
                : "You're running the latest version of EasyExtractUnitypackage!";
        });

        if (ConfigHelper.Config.Update.AutoUpdate && updateAvailable) await _updateHandler.Update();

        //EasterEggHeader
        EasterEggHeader.Visibility = ConfigHelper.Config.EasterEggHeader ? Visibility.Visible : Visibility.Collapsed;

        if (ConfigHelper.Config.Runs is { IsFirstRun: true })
        {
            NavView.Navigate(typeof(About));
            await _logger.LogAsync("First run detected, navigating to About", "Dashboard.xaml.cs", Importance.Info);
            ConfigHelper.Config.Runs.IsFirstRun = false;
            await ConfigHelper.UpdateConfigAsync();
        }
        else
        {
            await _logger.LogAsync("Navigating to Extraction", "Dashboard.xaml.cs", Importance.Info);
            NavView.Navigate(typeof(UserControls.Extraction));
        }

        if (ConfigHelper.Config.UwUModeActive)
        {
            NavView.Opacity = 0.2;
            TitleBar.Title = "EasyExtractUwUnitypackage";
            Title = "EasyExtractUwUnitypackage";
            await UwUAnimation();
        }
        else
        {
            TitleBar.Title = ConfigHelper.Config.AppTitle;
            Title = ConfigHelper.Config.AppTitle;
        }
    }

    private async Task UwUAnimation()
    {
        var pastelForeground = new SolidColorBrush(Color.FromArgb(255, 237, 187, 238));
        var textBlock = new TextBlock
        {
            Text = "UwU Mode Activated!",
            FontSize = 24,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = pastelForeground,
            Opacity = 0,
            TextAlignment = TextAlignment.Center,
            RenderTransform = new ScaleTransform(1, 1),
            Margin = new Thickness(0, 0, 0, 50)
        };

        Grid.SetRowSpan(textBlock, int.MaxValue);
        Grid.SetColumnSpan(textBlock, int.MaxValue);
        MainGrid.Children.Add(textBlock);
        var fadeInOutAnimation = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromSeconds(1)))
        {
            AutoReverse = true,
            EasingFunction = new SineEase
            {
                EasingMode = EasingMode.EaseInOut
            }
        };

        var storyboard = new Storyboard();
        storyboard.Children.Add(fadeInOutAnimation);
        Storyboard.SetTarget(fadeInOutAnimation, textBlock);
        Storyboard.SetTargetProperty(fadeInOutAnimation, new PropertyPath("Opacity"));

        textBlock.BeginAnimation(OpacityProperty, fadeInOutAnimation);


        storyboard.Completed += (sender, args) => { MainGrid.Children.Remove(textBlock); };
        storyboard.Begin();
        await Task.Delay(1000);
        NavView.Opacity = 1;
    }

    private async void Dashboard_OnDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        var files = (string[])e.Data.GetData(DataFormats.FileDrop);
        foreach (var file in files)
        {
            await _logger.LogAsync($"Dropped file: {file}", "Dashboard.xaml.cs", Importance.Info);
            var name = Path.GetFileName(file);
            var duplicate = UserControls.Extraction._queueList?.Find(x => x.UnityPackageName == name);
            if (duplicate != null) continue;
            UserControls.Extraction._queueList ??= new List<SearchEverythingModel>();
            UserControls.Extraction._queueList.Add(new SearchEverythingModel
            {
                UnityPackageName = name,
                UnityPackagePath = file,
                Id = 0
            });
        }

        await _logger.LogAsync("Added dropped files to queue", "Dashboard.xaml.cs", Importance.Info);
    }


    private async void CheckForUpdatesNavBtn_OnClick(object sender, RoutedEventArgs e)
    {
        var isUpToDate = await _updateHandler.IsUpToDate();
        var updateAvailable = !isUpToDate;

        await Dispatcher.InvokeAsync(() =>
        {
            CheckForUpdatesTxt.Text = updateAvailable ? "New Update Available" : "Check for Updates";
            CheckForUpdatesTxt.Foreground =
                new SolidColorBrush(updateAvailable ? Color.FromRgb(255, 0, 0) : Color.FromRgb(0, 255, 0));
            CheckForUpdatesDesc.Text = updateAvailable
                ? "Click here to update EasyExtractUnitypackage!"
                : "You're running the latest version of EasyExtractUnitypackage!";
        });

        if (updateAvailable) await _updateHandler.Update();
    }

    private async void DontShowAgainBtn_OnClick(object sender, RoutedEventArgs e)
    {
        //EasterEggHeader
        ConfigHelper.Config.EasterEggHeader = false;
        await ConfigHelper.UpdateConfigAsync();
        EasterEggHeader.Visibility = Visibility.Collapsed;
    }
}