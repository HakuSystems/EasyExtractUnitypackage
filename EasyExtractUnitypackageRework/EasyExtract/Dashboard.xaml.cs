using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using EasyExtract.Config;
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
    private readonly UpdateHandler UpdateHandler = new();

    public Dashboard()
    {
        InitializeComponent();
        DataContext = Config;
        ContentFrame = new UserControls.Extraction();
    }

    public static Dashboard Instance => instance ??= new Dashboard();
    private ConfigModel Config { get; } = new();


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
        var config = await ConfigHelper.LoadConfigAsync();
        var theme = config.ApplicationTheme;
        switch (theme)
        {
            case ApplicationTheme.Dark:
                ApplicationThemeManager.Apply(ApplicationTheme.Dark);
                break;
            case ApplicationTheme.Light:
                ApplicationThemeManager.Apply(ApplicationTheme.Light);
                break;
            case ApplicationTheme.Unknown:
                ApplicationThemeManager.Apply(ApplicationTheme.Dark);
                break;
            case ApplicationTheme.HighContrast:
                ApplicationThemeManager.Apply(ApplicationTheme.HighContrast);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        if (config.AutoUpdate)
        {
            var updateAvailable = await UpdateHandler.IsUptoDate();

            if (!updateAvailable) await UpdateHandler.Update();

            await Dispatcher.InvokeAsync(() =>
            {
                CheckForUpdatesTxt.Text = updateAvailable ? "New Update Available" : "Check for Updates";
                CheckForUpdatesTxt.Foreground =
                    new SolidColorBrush(updateAvailable ? Color.FromRgb(255, 0, 0) : Color.FromRgb(0, 255, 0));
                CheckForUpdatesDesc.Text = updateAvailable
                    ? "Click here to update EasyExtractUnitypackage!"
                    : "You're running the latest version of EasyExtractUnitypackage!";
            });
        }
        else
        {
            var updateAvailable = await UpdateHandler.IsUptoDate();

            await Dispatcher.InvokeAsync(() =>
            {
                CheckForUpdatesTxt.Text = updateAvailable ? "New Update Available" : "Check for Updates";
                CheckForUpdatesTxt.Foreground =
                    new SolidColorBrush(updateAvailable ? Color.FromRgb(255, 0, 0) : Color.FromRgb(0, 255, 0));
                CheckForUpdatesDesc.Text = updateAvailable
                    ? "Click here to update EasyExtractUnitypackage!"
                    : "You're running the latest version of EasyExtractUnitypackage!";
            });
        }

        switch (config.IsFirstRun)
        {
            case true:
                NavView.Navigate(typeof(About));
                config.IsFirstRun = false;
                await ConfigHelper.UpdateConfigAsync(config);
                break;
            default:
                NavView.Navigate(ContentFrame.GetType());
                break;
        }

        switch (config.UwUModeActive)
        {
            case true:
                NavView.Opacity = 0.2;
                TitleBar.Title = config.AppTitle;
                Title = config.AppTitle;
                await UwUAnimation();
                break;
            default:
                TitleBar.Title = config.AppTitle;
                Title = config.AppTitle;
                break;
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
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
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

    private void Dashboard_OnDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        var files = (string[])e.Data.GetData(DataFormats.FileDrop);
        foreach (var file in files)
        {
            var name = Path.GetFileName(file);
            var duplicate = UserControls.Extraction._queueList?.Find(x => x.UnityPackageName == name);
            if (duplicate != null) continue;
            if (UserControls.Extraction._queueList == null)
                UserControls.Extraction._queueList = new List<SearchEverythingModel>();
            UserControls.Extraction._queueList.Add(new SearchEverythingModel
                { UnityPackageName = name, UnityPackagePath = file, Id = 0 });
        }
    }


    private async void CheckForUpdatesNavBtn_OnClick(object sender, RoutedEventArgs e)
    {
        var updateAvailable = await UpdateHandler.IsUptoDate();

        await Dispatcher.InvokeAsync(() =>
        {
            CheckForUpdatesTxt.Text = updateAvailable ? "New Update Available" : "Check for Updates";
            CheckForUpdatesTxt.Foreground =
                new SolidColorBrush(updateAvailable ? Color.FromRgb(255, 0, 0) : Color.FromRgb(0, 255, 0));
            CheckForUpdatesDesc.Text = updateAvailable
                ? "Click here to update EasyExtractUnitypackage!"
                : "You're running the latest version of EasyExtractUnitypackage!";
        });
    }
}