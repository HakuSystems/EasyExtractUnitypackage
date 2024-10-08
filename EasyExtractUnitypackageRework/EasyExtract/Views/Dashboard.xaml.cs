using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using EasyExtract.Config;
using EasyExtract.Controls;
using EasyExtract.Models;
using EasyExtract.Services;
using EasyExtract.Utilities;
using Wpf.Ui.Controls;
using Application = System.Windows.Application;
using Color = System.Windows.Media.Color;
using TextBlock = Wpf.Ui.Controls.TextBlock;

namespace EasyExtract.Views;

public partial class Dashboard
{
    private static Dashboard? _instance;
    private readonly BackgroundManager _backgroundManager = BackgroundManager.Instance;
    private readonly ConfigHelper _configHelper = new();
    private readonly UpdateHandler _updateHandler = new();

    public Dashboard()
    {
        InitializeComponent();
        DataContext = this;
    }

    public static Dashboard Instance
    {
        get => _instance ??= new Dashboard();
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
        await _configHelper.ReadConfigAsync();
        VersionTxt.Content = "V" + Application.ResourceAssembly.GetName().Version;
        _backgroundManager.UpdateBackground(_configHelper.Config.Backgrounds.BackgroundPath);
        _backgroundManager.UpdateOpacity(_configHelper.Config.Backgrounds.BackgroundOpacity);

        var theme = _configHelper.Config.ApplicationTheme;
        switch (theme)
        {
            case ApplicationTheme.Dark:
                ApplicationThemeManager.Apply(ApplicationTheme.Dark);
                await BetterLogger.LogAsync("Applied Dark Theme", $"{nameof(Dashboard)}.xaml.cs", Importance.Info);
                break;
            case ApplicationTheme.Light:
                ApplicationThemeManager.Apply(ApplicationTheme.Light);
                await BetterLogger.LogAsync("Applied Light Theme", $"{nameof(Dashboard)}.xaml.cs", Importance.Info);
                break;
            case ApplicationTheme.HighContrast:
                ApplicationThemeManager.Apply(ApplicationTheme.HighContrast);
                await BetterLogger.LogAsync("Applied High Contrast Theme", $"{nameof(Dashboard)}.xaml.cs", Importance.Info);
                break;
            default:
                ApplicationThemeManager.Apply(ApplicationTheme.Dark);
                await BetterLogger.LogAsync("Applied Dark Theme", $"{nameof(Dashboard)}.xaml.cs", Importance.Info)
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

        if (_configHelper.Config.Update.AutoUpdate && updateAvailable) await _updateHandler.Update();

        //EasterEggHeader
        EasterEggHeader.Visibility = _configHelper.Config.EasterEggHeader ? Visibility.Visible : Visibility.Collapsed;

        if (_configHelper.Config.Runs is { IsFirstRun: true })
        {
            NavView.Navigate(typeof(About));
            await BetterLogger.LogAsync("First run detected, navigating to About", $"{nameof(Dashboard)}.xaml.cs", Importance.Info);
            _configHelper.Config.Runs.IsFirstRun = false;
            await _configHelper.UpdateConfigAsync();
        }
        else
        {
            await BetterLogger.LogAsync("Navigating to Extraction", $"{nameof(Dashboard)}.xaml.cs", Importance.Info);
            NavView.Navigate(typeof(Extraction));
        }

        if (_configHelper.Config.UwUModeActive)
        {
            NavView.Opacity = 0.2;
            TitleBar.Title = "EasyExtractUwUnitypackage";
            Title = "EasyExtractUwUnitypackage";
            await UwUAnimation();
        }
        else
        {
            TitleBar.Title = _configHelper.Config.AppTitle;
            Title = _configHelper.Config.AppTitle;
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
        Storyboard.SetTargetProperty(fadeInOutAnimation, new PropertyPath(OpacityProperty));

        textBlock.BeginAnimation(OpacityProperty, fadeInOutAnimation);


        storyboard.Completed += (_, _) => { MainGrid.Children.Remove(textBlock); };
        storyboard.Begin();
        await Task.Delay(1000);
        NavView.Opacity = 1;
    }

    private async void Dashboard_OnDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
            foreach (var file in files)
            {
                await BetterLogger.LogAsync($"Dropped file: {file}", "Dashboard.xaml.cs", Importance.Info);
                var name = Path.GetFileName(file);
                var duplicate = Extraction.QueueList?.Find(x => x.UnityPackageName == name);
                if (duplicate != null) continue;
                Extraction.QueueList ??= new List<SearchEverythingModel>();
                Extraction.QueueList.Add(new SearchEverythingModel
                {
                    UnityPackageName = name,
                    UnityPackagePath = file,
                    Id = 0
                });
            }

        await BetterLogger.LogAsync("Added dropped files to queue", "Dashboard.xaml.cs", Importance.Info);
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
        _configHelper.Config.EasterEggHeader = false;
        await _configHelper.UpdateConfigAsync();
        EasterEggHeader.Visibility = Visibility.Collapsed;
    }
}