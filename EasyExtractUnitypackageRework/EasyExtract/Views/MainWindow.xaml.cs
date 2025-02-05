using EasyExtract.Config;
using EasyExtract.Config.Models;
using EasyExtract.Utilities;

namespace EasyExtract.Views;

/// <summary>
///     Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private async void AnimationBehavior_OnLoaded(object sender, RoutedEventArgs e)
    {
        await BetterLogger.LogAsync("Application started", Importance.Info);

        if (!ConfigHandler.Instance.Config.IntroLogoAnimation)
        {
            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                new Dashboard().Show();
                Close();
            };
            timer.Start();
        }
        else
        {
            new Dashboard().Show();
            Close();
        }
    }

    private void MainWindow_OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        // no config call, since this should be always enabled
        var scaleFactor = e.NewSize.Width / 800.0;

        if (scaleFactor < 1.0)
        {
            LogoImage.Width = 400 * scaleFactor;
            LogoImage.Height = 200 * scaleFactor;
        }
        else
        {
            LogoImage.Width = double.NaN;
            LogoImage.Height = double.NaN;
        }
    }
}