using EasyExtract.Config;
using EasyExtract.Services.Discord;
using EasyExtract.Utilities;

namespace EasyExtract.UI.Initial;

/// <summary>
///     Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow
{
    private readonly ConfigHelper _configHelper = new();

    public MainWindow()
    {
        InitializeComponent();
    }

    private async void AnimationBehavior_OnLoaded(object sender, RoutedEventArgs e)
    {
        await BetterLogger.LogAsync("Application started", $"{nameof(MainWindow)}.xaml.cs", Importance.Info);
        await DiscordRpcManager.Instance.DiscordStart();
        await GenerateAllNecessaryFiles();

        if (!_configHelper.Config.IntroLogoAnimation)
        {
            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            timer.Tick += (_, _) =>
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

    private static async Task GenerateAllNecessaryFiles()
    {
        //Appdata folder
        var appdata = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appdataFolder = Path.Combine(appdata, "EasyExtract");
        if (!Directory.Exists(appdataFolder)) Directory.CreateDirectory(appdataFolder);

        //EasyExtract\Extracted
        var extractedFolder = Path.Combine(appdataFolder, "Extracted");
        if (!Directory.Exists(extractedFolder)) Directory.CreateDirectory(extractedFolder);

        //EasyExtract\Temp
        var tempFolder = Path.Combine(appdataFolder, "Temp");
        if (!Directory.Exists(tempFolder)) Directory.CreateDirectory(tempFolder);

        //EasyExtract\ThirdParty
        var thirdPartyFolder = Path.Combine(appdataFolder, "ThirdParty");
        if (!Directory.Exists(thirdPartyFolder)) Directory.CreateDirectory(thirdPartyFolder);

        //EasyExtract\IgnoredUnity packages
        var ignoredUnityPackagesFolder = Path.Combine(appdataFolder, "IgnoredUnitypackages");
        if (!Directory.Exists(ignoredUnityPackagesFolder)) Directory.CreateDirectory(ignoredUnityPackagesFolder);
    }
}