using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using EasyExtract.Config;
using EasyExtract.Config.Models;
using EasyExtract.Controls;
using EasyExtract.Services;
using EasyExtract.Utilities;
using Wpf.Ui.Controls;
using Application = System.Windows.Application;
using Color = System.Windows.Media.Color;
using Size = System.Windows.Size;
using TextBlock = Wpf.Ui.Controls.TextBlock;

namespace EasyExtract.Views;

public partial class Dashboard : Window
{
    private static Dashboard? _instance;
    private readonly BackgroundManager _backgroundManager = BackgroundManager.Instance;
    private readonly UpdateHandler _updateHandler = new();
    private readonly Random random = new();

    private DispatcherTimer? sparkleTimer;

    public Dashboard()
    {
        InitializeComponent();
        ThemeMode = ThemeMode.System;
    }

    public static Dashboard Instance => _instance ??= new Dashboard();

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
        var theme = ConfigHandler.Instance.Config.ApplicationTheme;
        var themeMode = theme switch
        {
            AvailableThemes.System => ThemeMode.System,
            AvailableThemes.Dark => ThemeMode.Dark,
            AvailableThemes.Light => ThemeMode.Light,
            _ => ThemeMode.System
        };
        Application.Current.ThemeMode = themeMode;
        ThemeMode = themeMode;
        VersionTxt.Content = "V" + Application.ResourceAssembly.GetName().Version;
        await _backgroundManager.UpdateBackground(ConfigHandler.Instance.Config.CustomBackgroundImage.BackgroundPath);
        await _backgroundManager.UpdateOpacity(ConfigHandler.Instance.Config.CustomBackgroundImage.BackgroundOpacity);

        var isUpToDate = await _updateHandler.IsUpToDateOrUpdate(false);
        var updateAvailable = !isUpToDate;

        await Dispatcher.InvokeAsync(() =>
        {
            CheckForUpdatesTxt.Text = updateAvailable ? "New Update Available" : "Check for Updates";
            CheckForUpdatesTxt.Foreground =
                new SolidColorBrush(updateAvailable ? Color.FromRgb(255, 0, 0) : Color.FromRgb(0, 255, 0));
            CheckForUpdatesDesc.Text = updateAvailable
                ? "Click here to update EasyExtractUnitypackage!"
                : "You're running the latest version of EasyExtractUnitypackage!";
            CheckForUpdatesNavBtn.IsEnabled = updateAvailable;
        });

        if (ConfigHandler.Instance.Config.Update.AutoUpdate && updateAvailable)
            await _updateHandler.IsUpToDateOrUpdate(true);

        EasterEggHeader.Visibility =
            ConfigHandler.Instance.Config.EasterEggHeader ? Visibility.Visible : Visibility.Collapsed;

        if (ConfigHandler.Instance.Config.FirstRun)
        {
            NavView.Navigate(typeof(About));
            await BetterLogger.LogAsync("First run detected, navigating to About", Importance.Info);
            ConfigHandler.Instance.Config.FirstRun = false;
        }
        else
        {
            await BetterLogger.LogAsync("Navigating to Extraction", Importance.Info);
            NavView.Navigate(typeof(Extraction));
        }

        if (ConfigHandler.Instance.Config.UwUModeActive)
        {
            NavView.Opacity = 0;
            await UwUAnimation();
            Title = BetterUwUifyer.UwUify(ConfigHandler.Instance.Config.AppTitle);
            BetterUwUifyer.ApplyUwUModeToVisualTree(this);
        }
        else
        {
            Title = ConfigHandler.Instance.Config.AppTitle;
        }
    }

    private async Task UwUAnimation()
    {
        var pastelForeground = new SolidColorBrush(Color.FromArgb(255, 237, 187, 238));

        var loadingText = new TextBlock
        {
            Text = "UwUifying your experience...",
            FontSize = 28,
            Foreground = pastelForeground,
            Opacity = 0,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            RenderTransform = new ScaleTransform(1, 1)
        };
        Grid.SetRowSpan(loadingText, int.MaxValue);
        Grid.SetColumnSpan(loadingText, int.MaxValue);
        MainGrid.Children.Add(loadingText);
        loadingText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        loadingText.Arrange(new Rect(loadingText.DesiredSize));
        Panel.SetZIndex(loadingText, 100);

        var fadeAnimation = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromSeconds(0.8),
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
        var scaleAnimation = new DoubleAnimation
        {
            From = 1,
            To = 1.1,
            Duration = TimeSpan.FromSeconds(0.8),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
        var loadingStoryboard = new Storyboard();
        loadingStoryboard.Children.Add(fadeAnimation);
        loadingStoryboard.Children.Add(scaleAnimation);
        Storyboard.SetTarget(fadeAnimation, loadingText);
        Storyboard.SetTargetProperty(fadeAnimation, new PropertyPath(OpacityProperty));
        Storyboard.SetTarget(scaleAnimation, loadingText.RenderTransform);
        Storyboard.SetTargetProperty(scaleAnimation, new PropertyPath(ScaleTransform.ScaleXProperty));
        loadingText.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation);
        loadingStoryboard.Begin();

        StartSparkleRain();

        await Task.Delay(TimeSpan.FromSeconds(5));

        StopSparkleRain();

        var fadeOutAnimation = new DoubleAnimation
        {
            From = loadingText.Opacity,
            To = 0,
            Duration = TimeSpan.FromSeconds(0.8),
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
        loadingText.BeginAnimation(OpacityProperty, fadeOutAnimation);

        await Task.Delay(TimeSpan.FromSeconds(1));
        NavView.Opacity = 1;
    }

    private void StartSparkleRain()
    {
        sparkleTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        sparkleTimer.Tick += (s, e) => SpawnSparkle();
        sparkleTimer.Start();
    }

    private void StopSparkleRain()
    {
        sparkleTimer?.Stop();
        sparkleTimer = null;
    }

    private void SpawnSparkle()
    {
        var pastelForeground = new SolidColorBrush(Color.FromArgb(255, 237, 187, 238));
        var sparkle = new TextBlock
        {
            Text = "✨",
            FontSize = random.Next(10, 30),
            Foreground = pastelForeground,
            Opacity = 0
        };

        Panel.SetZIndex(sparkle, 0);
        MainGrid.Children.Add(sparkle);

        var translate = new TranslateTransform
        {
            X = random.NextDouble() * MainGrid.ActualWidth,
            Y = random.NextDouble() * MainGrid.ActualHeight
        };
        sparkle.RenderTransform = translate;

        var fallDistance = MainGrid.ActualHeight + sparkle.ActualHeight;
        var fallDuration = TimeSpan.FromSeconds(random.NextDouble() * 2 + 3); // 3-5 seconds
        var fallAnimation = new DoubleAnimation
        {
            From = translate.Y,
            To = fallDistance,
            Duration = fallDuration,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseIn }
        };

        var driftDistance = random.NextDouble() * 1200 - 600; // Increase drift to make it more noticeable
        var driftAnimation = new DoubleAnimation
        {
            From = translate.X,
            To = translate.X + driftDistance,
            Duration = fallDuration,
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };

        var opacityAnimation = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromSeconds(0.5),
            AutoReverse = true
        };

        var sb = new Storyboard();
        sb.Children.Add(fallAnimation);
        sb.Children.Add(driftAnimation);
        sb.Children.Add(opacityAnimation);
        Storyboard.SetTarget(fallAnimation, translate);
        Storyboard.SetTargetProperty(fallAnimation, new PropertyPath(TranslateTransform.YProperty));
        Storyboard.SetTarget(driftAnimation, translate);
        Storyboard.SetTargetProperty(driftAnimation, new PropertyPath(TranslateTransform.XProperty));
        Storyboard.SetTarget(opacityAnimation, sparkle);
        Storyboard.SetTargetProperty(opacityAnimation, new PropertyPath(OpacityProperty));
        sb.Completed += (s, e) => MainGrid.Children.Remove(sparkle);
        sb.Begin();
    }

    private async void Dashboard_OnDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return;
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
            foreach (var file in files)
            {
                await BetterLogger.LogAsync($"Dropped file: {file}", Importance.Info);
                var name = Path.GetFileName(file);
                var duplicate = Extraction.QueueList?.Find(x => x.UnityPackageName == name);
                if (duplicate != null)
                    continue;
                Extraction.QueueList ??= new List<SearchEverythingModel>();
                Extraction.QueueList.Add(new SearchEverythingModel
                {
                    UnityPackageName = name,
                    UnityPackagePath = file,
                    Id = 0
                });
            }

        await BetterLogger.LogAsync("Added dropped files to queue", Importance.Info);
    }

    private async void CheckForUpdatesNavBtn_OnClick(object sender, RoutedEventArgs e)
    {
        var isUpToDate = await _updateHandler.IsUpToDateOrUpdate(false);
        var updateAvailable = !isUpToDate;

        await Dispatcher.InvokeAsync(() =>
        {
            CheckForUpdatesTxt.Text = updateAvailable ? "New Update Available" : "Check for Updates";
            CheckForUpdatesTxt.Foreground =
                new SolidColorBrush(updateAvailable ? Color.FromRgb(255, 0, 0) : Color.FromRgb(0, 255, 0));
            CheckForUpdatesDesc.Text = updateAvailable
                ? "Click here to update EasyExtractUnitypackage!"
                : "You're running the latest version of EasyExtractUnitypackage!";
            CheckForUpdatesNavBtn.IsEnabled = updateAvailable;
        });

        if (updateAvailable)
            await _updateHandler.IsUpToDateOrUpdate(true);
    }

    private async void DontShowAgainBtn_OnClick(object sender, RoutedEventArgs e)
    {
        ConfigHandler.Instance.Config.EasterEggHeader = false;
        EasterEggHeader.Visibility = Visibility.Collapsed;
    }

    private void Dashboard_OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        switch (ConfigHandler.Instance.Config.DynamicScalingMode)
        {
            case DynamicScalingModes.Off:
                DialogHelperGrid.LayoutTransform = Transform.Identity;
                return;
            case DynamicScalingModes.Simple:
            case DynamicScalingModes.Experimental:
            {
                var scaleFactor = e.NewSize.Width / 1600.0;
                if (scaleFactor < 0.5)
                    scaleFactor = 0.5;
                else if (scaleFactor > 2.0)
                    scaleFactor = 2.0;
                DialogHelperGrid.LayoutTransform = new ScaleTransform(scaleFactor, scaleFactor);
                break;
            }
        }
    }
}