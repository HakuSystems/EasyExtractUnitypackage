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
using TextBlock = Wpf.Ui.Controls.TextBlock;

namespace EasyExtract.Views;

public partial class Dashboard : Window
{
    private static Dashboard? _instance;
    private readonly BackgroundManager _backgroundManager = BackgroundManager.Instance;
    private readonly UpdateHandler _updateHandler = new();

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
        await _backgroundManager.UpdateBackground(ConfigHandler.Instance.Config.Backgrounds.BackgroundPath);
        await _backgroundManager.UpdateOpacity(ConfigHandler.Instance.Config.Backgrounds.BackgroundOpacity);

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

        //EasterEggHeader
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
            NavView.Opacity = 0.2;
            Title = "EasyExtractUwUnitypackage";
            await UwUAnimation();
        }
        else
        {
            Title = ConfigHandler.Instance.Config.AppTitle;
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

        var fadeInOutAnimation = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = new Duration(TimeSpan.FromSeconds(1)),
            AutoReverse = true,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };

        var scaleAnimation = new DoubleAnimation
        {
            From = 1,
            To = 1.2,
            Duration = new Duration(TimeSpan.FromSeconds(1)),
            AutoReverse = true,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };

        var storyboard = new Storyboard();
        storyboard.Children.Add(fadeInOutAnimation);
        storyboard.Children.Add(scaleAnimation);

        Storyboard.SetTarget(fadeInOutAnimation, textBlock);
        Storyboard.SetTargetProperty(fadeInOutAnimation, new PropertyPath(OpacityProperty));

        Storyboard.SetTarget(scaleAnimation, textBlock.RenderTransform);
        Storyboard.SetTargetProperty(scaleAnimation, new PropertyPath(ScaleTransform.ScaleXProperty));

        // Link X and Y scaling together for uniform scaling
        textBlock.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation);

        storyboard.Completed += (_, _) => { MainGrid.Children.Remove(textBlock); };
        storyboard.Begin();

        await Task.Delay(TimeSpan.FromSeconds(2));
        NavView.Opacity = 1;
    }

    private async void Dashboard_OnDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
            foreach (var file in files)
            {
                await BetterLogger.LogAsync($"Dropped file: {file}", Importance.Info);
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

        if (updateAvailable) await _updateHandler.IsUpToDateOrUpdate(true);
    }

    private async void DontShowAgainBtn_OnClick(object sender, RoutedEventArgs e)
    {
        //EasterEggHeader
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
            {
                var scaleFactor = e.NewSize.Width / 1600.0;

                switch (scaleFactor)
                {
                    case < 0.5:
                        scaleFactor = 0.5;
                        break;
                    case > 2.0:
                        scaleFactor = 2.0;
                        break;
                }

                DialogHelperGrid.LayoutTransform = new ScaleTransform(scaleFactor, scaleFactor);
                break;
            }
            case DynamicScalingModes.Experimental:
            {
                var scaleFactor = e.NewSize.Width / 1600.0;

                switch (scaleFactor)
                {
                    case < 0.5:
                        scaleFactor = 0.5;
                        break;
                    case > 2.0:
                        scaleFactor = 2.0;
                        break;
                }

                DialogHelperGrid.LayoutTransform = new ScaleTransform(scaleFactor, scaleFactor);
                break;
            }
        }
    }
}