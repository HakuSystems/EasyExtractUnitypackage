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
}