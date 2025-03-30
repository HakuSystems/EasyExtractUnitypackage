using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using EasyExtract.BetterExtraction;
using EasyExtract.Config;
using EasyExtract.Config.Models;
using EasyExtract.Controls;
using EasyExtract.Services;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using Application = System.Windows.Application;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Size = System.Windows.Size;
using TextBlock = Wpf.Ui.Controls.TextBlock;

namespace EasyExtract.Views;

public partial class Dashboard : Window
{
    private static Dashboard? _instance;
    private readonly BackgroundManager _backgroundManager = BackgroundManager.Instance;
    private readonly FilterQueue _filterQueue = new();

    private readonly HashChecks _hashChecks = new();

    private readonly TimeSpan _resetDelayForDrop = TimeSpan.FromSeconds(3);
    private readonly UpdateHandler _updateHandler = new();
    private readonly Random random = new();
    private CancellationTokenSource _dragDropResetToken;

    private DispatcherTimer? sparkleTimer;

    public Dashboard()
    {
        InitializeComponent();
        if (_instance == null)
            _instance = this;
        ThemeMode = ThemeMode.System;
    }

    public static Dashboard Instance => _instance
                                        ?? throw new InvalidOperationException(
                                            "Dashboard has not yet been initialized.");


    private async void Dashboard_OnLoaded(object sender, RoutedEventArgs e)
    {
        var config = ConfigHandler.Instance.Config;

        // Set window state and scale immediately
        if (Enum.TryParse(config.WindowState, out WindowState state))
            WindowState = state;

        // Now continue with async logic
        var themeMode = config.ApplicationTheme switch
        {
            AvailableThemes.System => ThemeMode.System,
            AvailableThemes.Dark => ThemeMode.Dark,
            AvailableThemes.Light => ThemeMode.Light,
            _ => ThemeMode.System
        };

        Application.Current.ThemeMode = themeMode;
        ThemeMode = themeMode;

        await _backgroundManager.UpdateBackground(config.CustomBackgroundImage.BackgroundPath);
        await _backgroundManager.UpdateOpacity(config.CustomBackgroundImage.BackgroundOpacity);

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

        if (config.Update.AutoUpdate && updateAvailable)
            if (await _updateHandler.IsUpToDateOrUpdate(true))
            {
                await DialogHelper.ShowInfoDialogAsync(this, "Update available",
                    "An update is available and will be installed automatically.\nPlease don't interact with the application until the update is installed. (The app will automatically restart.)");
                CurrentlyUpdatingTextBlock.Visibility = Visibility.Visible;
            }

        if (config.FirstRun)
        {
            NavView.Navigate(typeof(BetterSettings));
            config.FirstRun = false;
        }
        else
        {
            NavView.Navigate(typeof(Controls.BetterExtraction));
        }

        if (config.UwUModeActive)
        {
            NavView.Opacity = 0;
            await UwUAnimation();
            Title = BetterUwUifyer.UwUify(config.AppTitle);
            BetterUwUifyer.ApplyUwUModeToVisualTree(this);
        }
        else
        {
            Title = config.AppTitle;
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
        NavView.Navigate(typeof(BetterDetails));
    }

    private void SettingsBtn_OnClick(object sender, RoutedEventArgs e)
    {
        NavView.Navigate(typeof(BetterSettings));
    }

    private void Dashboard_OnDragLeave(object sender, DragEventArgs e)
    {
        UpdateDragDropText("Move your Unitypackage somewhere else to drop it", DragDropColors.DragLeave);
        ScheduleTextReset();
    }

    private void Dashboard_OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;

        e.Handled = true;
        CancelPendingReset();
        UpdateDragDropText("Ready to drop", DragDropColors.DragOver);
    }

    private void Dashboard_OnDrop(object sender, DragEventArgs e)
    {
        UpdateDragDropText("Added to queue!", DragDropColors.Dropped);
        ScheduleTextReset();
        AddToQueue(e);
    }

    private async void AddToQueue(DragEventArgs dragEventArgs)
    {
        if (!dragEventArgs.Data.GetDataPresent(DataFormats.FileDrop))
            return;

        var droppedFiles = (string[])dragEventArgs.Data.GetData(DataFormats.FileDrop);

        var unitypackageFiles = droppedFiles
            .Where(file => file.EndsWith(".unitypackage", StringComparison.OrdinalIgnoreCase))
            .Select(filePath => new FileInfo(filePath))
            .ToList();

        if (!unitypackageFiles.Any())
        {
            UpdateDragDropText("No valid Unitypackage files found!", DragDropColors.DragLeave);
            ScheduleTextReset();
            return;
        }

        var fileDetails = await Task.Run(() => unitypackageFiles.Select(file =>
        {
            var hash = _hashChecks.ComputeFileHash(file);
            return new UnitypackageFileInfo
            {
                FileName = file.Name,
                FileHash = hash,
                FileSize = file.Length.ToString(),
                FileDate = file.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"),
                FilePath = file.FullName,
                FileExtension = file.Extension,
                IsInQueue = true
            };
        }).ToList());

        ConfigHandler.Instance.Config.UnitypackageFiles.AddRange(fileDetails);
        _filterQueue.FilterDuplicates();
    }


    private void UpdateDragDropText(string text, string colorHex, double opacity = 1)
    {
        DragDropDetectionTxt.Text = text;
        DragDropDetectionTxt.Foreground = new SolidColorBrush(
            (Color)ColorConverter.ConvertFromString(colorHex));
        DragDropDetectionTxt.Opacity = opacity;
    }

    private void ScheduleTextReset()
    {
        CancelPendingReset();

        _dragDropResetToken = new CancellationTokenSource();
        var token = _dragDropResetToken.Token;

        Task.Delay(_resetDelayForDrop, token).ContinueWith(task =>
        {
            if (task.IsCanceled) return;

            Dispatcher.Invoke(ResetDragDropText);
        }, token);
    }

    private void CancelPendingReset()
    {
        _dragDropResetToken?.Cancel();
        _dragDropResetToken?.Dispose();
        _dragDropResetToken = null;
    }

    private void ResetDragDropText()
    {
        UpdateDragDropText("Drag and Drop is Supported!", DragDropColors.DefaultText);
    }


    private void DetailsCard_OnMouseEnter(object sender, MouseEventArgs e)
    {
        DetailsBtnFocusPoint.Visibility = Visibility.Visible;
    }

    private void DetailsCard_OnMouseLeave(object sender, MouseEventArgs e)
    {
        DetailsBtnFocusPoint.Visibility = Visibility.Hidden;
    }

    private void FeedbackBtnFooter_OnClick(object sender, RoutedEventArgs e)
    {
        NavView.Navigate(typeof(Feedback));
    }

    private void SettingsBtnFooter_OnClick(object sender, RoutedEventArgs e)
    {
        NavView.Navigate(typeof(BetterSettings));
    }

    private void GradientCanvas_OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        float width = e.Info.Width;
        float height = e.Info.Height;

        using var paint = new SKPaint
        {
            Shader = SKShader.CreateRadialGradient(
                new SKPoint(width / 2, height), // Bottom-center position
                height, // Larger radius to clearly fill
                new[]
                {
                    SKColor.Parse(ConfigHandler.Instance.Config.PrimaryColorHex).WithAlpha(240),
                    SKColor.Parse(ConfigHandler.Instance.Config.PrimaryColorHex).WithAlpha(0)
                },
                new[] { 0f, 1f },
                SKShaderTileMode.Clamp),
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 150),
            BlendMode = SKBlendMode.SoftLight,
            IsAntialias = true
        };

        // Clearly visible glow circle at bottom-center
        canvas.DrawCircle(width / 2, height, height, paint);
    }

    private void NavigateBackBtn_OnClick(object sender, RoutedEventArgs e)
    {
        NavView.GoBack();
    }

    private void Dashboard_OnClosing(object? sender, CancelEventArgs e)
    {
        var config = ConfigHandler.Instance.Config;

        config.WindowTop = Top;
        config.WindowLeft = Left;
        config.WindowWidth = Width;
        config.WindowHeight = Height;
        config.WindowState = WindowState.ToString();

        ConfigHandler.Instance.OverrideConfig();
    }
}