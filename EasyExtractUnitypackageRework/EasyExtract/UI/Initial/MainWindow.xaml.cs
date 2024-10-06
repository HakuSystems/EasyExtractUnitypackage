using EasyExtract.Config;
using EasyExtract.Services.Discord;
using EasyExtract.Utilities;

namespace EasyExtract.UI.Initial;

/// <summary>
///     Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly ConfigHelper _configHelper = new();
    private readonly BetterLogger _logger = new();

    public MainWindow()
    {
        InitializeComponent();
    }

    private async void AnimationBehavior_OnLoaded(object sender, RoutedEventArgs e)
    {
        await _logger.LogAsync("Application started", "MainWindow.xaml.cs", Importance.Info);
        DiscordRpcManager.Instance.DiscordStart();
        await GenerateAllNessesaryFiles();

        if (!_configHelper.Config.IntroLogoAnimation)
        {
            var timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(5);
            timer.Tick += (sender, args) =>
            {
                timer.Stop();
                new Dashboard.Dashboard().Show();
                Close();
            };
            timer.Start();
        }
        else
        {
            new Dashboard.Dashboard().Show();
            Close();
        }
    }

    private async Task GenerateAllNessesaryFiles()
    {
        //Appdata folder
        var appdata = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appdataFolder = Path.Combine(appdata, "EasyExtract");
        if (!Directory.Exists(appdataFolder)) Directory.CreateDirectory(appdataFolder);
        await _logger.LogAsync("Appdata folder created", "MainWindow.xaml.cs", Importance.Info);

        //EasyExtract\Extracted
        var extractedFolder = Path.Combine(appdataFolder, "Extracted");
        if (!Directory.Exists(extractedFolder)) Directory.CreateDirectory(extractedFolder);
        await _logger.LogAsync("Extracted folder created", "MainWindow.xaml.cs", Importance.Info);

        //EasyExtract\Temp
        var tempFolder = Path.Combine(appdataFolder, "Temp");
        if (!Directory.Exists(tempFolder)) Directory.CreateDirectory(tempFolder);
        await _logger.LogAsync("Temp folder created", "MainWindow.xaml.cs", Importance.Info);

        //EasyExtract\ThirdParty
        var thirdPartyFolder = Path.Combine(appdataFolder, "ThirdParty");
        if (!Directory.Exists(thirdPartyFolder)) Directory.CreateDirectory(thirdPartyFolder);
        await _logger.LogAsync("ThirdParty folder created", "MainWindow.xaml.cs", Importance.Info);

        //EasyExtract\IgnoredUnitypackages
        var ignoredUnitypackagesFolder = Path.Combine(appdataFolder, "IgnoredUnitypackages");
        if (!Directory.Exists(ignoredUnitypackagesFolder)) Directory.CreateDirectory(ignoredUnitypackagesFolder);
        await _logger.LogAsync("IgnoredUnitypackages folder created", "MainWindow.xaml.cs", Importance.Info);
    }
}