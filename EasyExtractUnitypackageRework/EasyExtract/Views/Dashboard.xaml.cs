using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using EasyExtract.Config;
using EasyExtract.Config.Models;
using EasyExtract.Controls;
using EasyExtract.Services;
using EasyExtract.Utilities;
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
        await _backgroundManager.UpdateBackground(ConfigHandler.Instance.Config.CustomBackgroundImage.BackgroundPath);
        await _backgroundManager.UpdateOpacity(ConfigHandler.Instance.Config.CustomBackgroundImage.BackgroundOpacity);

        var updateAvailable = !await _updateHandler.IsUpToDateOrUpdate(false);

        await Dispatcher.InvokeAsync(() =>
        {
            CheckForUpdatesDesc.Text = updateAvailable
                ? "An update is available. Click here to update."
                : "You are up to date.";
            if (!updateAvailable) return;
            CheckForUpdatesDesc.TextDecorations = TextDecorations.Underline;
            CheckForUpdatesDesc.Cursor = Cursors.Hand;
            CheckForUpdatesDesc.Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 0, 0));
        });

        if (ConfigHandler.Instance.Config.Update.AutoUpdate && updateAvailable)
        {
            await DialogHelper.ShowInfoDialogAsync(this, "Update available",
                "An update is available and will be installed automatically.\nbecause you have enabled automatic updates in the settings.\nPlease Dont Interact with the application until the update is installed. (the app will automatically restart)");
            CurrentlyUpdatingTextBlock.Visibility = Visibility.Visible;
            await _updateHandler.IsUpToDateOrUpdate(true);
        }

        if (ConfigHandler.Instance.Config.FirstRun)
        {
            NavView.Navigate(typeof(BetterSettings));
            ConfigHandler.Instance.Config.FirstRun = false;
        }
        else
        {
            NavView.Navigate(typeof(Controls.BetterExtraction));
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
                var duplicate = Extraction.QueueList?.Find(x => x.FileName == name);
                if (duplicate != null)
                    continue;
                Extraction.QueueList ??= new List<SearchEverythingModel>();
                Extraction.QueueList.Add(new SearchEverythingModel
                {
                    FileName = name,
                    FilePath = file,
                    Id = 0
                });
            }

        await BetterLogger.LogAsync("Added dropped files to queue", Importance.Info);
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

    private void UpdateTextBlock_OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        //pink text blinking
        var pink = new SolidColorBrush(Color.FromArgb(255, 237, 187, 238));
        var pinkAnimation = new ColorAnimation
        {
            From = pink.Color,
            To = Colors.White,
            Duration = TimeSpan.FromSeconds(0.3),
            AutoReverse = true,
            RepeatBehavior = new RepeatBehavior(3)
        };

        UpdateTextBlock.Foreground = pink;
        UpdateTextBlock.Foreground.BeginAnimation(SolidColorBrush.ColorProperty, pinkAnimation);


        NavView.Navigate(typeof(EasterEgg));
    }

    private async void CheckForUpdatesDesc_OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        var updateAvailable = !await _updateHandler.IsUpToDateOrUpdate(false);
        if (updateAvailable)
        {
            CurrentlyUpdatingTextBlock.Visibility = Visibility.Visible;
            await _updateHandler.IsUpToDateOrUpdate(true);
        }
    }

    private void DetailsBtn_OnClick(object sender, RoutedEventArgs e)
    {
        NavView.Navigate(typeof(History));
    }

    private void SettingsBtn_OnClick(object sender, RoutedEventArgs e)
    {
        NavView.Navigate(typeof(BetterSettings));
    }
}