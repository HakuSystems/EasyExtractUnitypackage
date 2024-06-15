using System.IO;
using System.Windows;
using System.Windows.Threading;
using EasyExtract.Discord;

namespace EasyExtract;

/// <summary>
///     Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private async void AnimationBehavior_OnLoaded(object sender, RoutedEventArgs e)
    {
        DiscordRpcManager.Instance.DiscordStart();
        GenerateAllNessesaryFiles();

        var timer = new DispatcherTimer();
        timer.Interval = TimeSpan.FromSeconds(5);
        timer.Tick += (sender, args) =>
        {
            timer.Stop();
            new Dashboard().Show();
            Close();
        };
        timer.Start();
    }

    private void GenerateAllNessesaryFiles()
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

        //EasyExtract\IgnoredUnitypackages
        var ignoredUnitypackagesFolder = Path.Combine(appdataFolder, "IgnoredUnitypackages");
        if (!Directory.Exists(ignoredUnitypackagesFolder)) Directory.CreateDirectory(ignoredUnitypackagesFolder);
    }
}