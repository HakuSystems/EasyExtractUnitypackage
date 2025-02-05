using EasyExtract.Config;
using EasyExtract.Services;
using EasyExtract.Utilities;
using Importance = EasyExtract.Config.Models.Importance;

namespace EasyExtract.Core;

/// <summary>
///     Interaction logic for App.xaml
/// </summary>
public partial class App
{
    private ContrastCheckerService _contrastChecker;
    private ThemeService _themeService;

    private static async void Application_DispatcherUnhandledException(object sender,
        DispatcherUnhandledExceptionEventArgs e)
    {
        await BetterLogger.LogAsync(e.Exception.Message, Importance.Error);
        e.Handled = true;
    }

    private async void App_OnExit(object sender, ExitEventArgs e)
    {
        DiscordRpcManager.Instance.Dispose();
        base.OnExit(e);
    }

    private async void App_OnStartup(object sender, StartupEventArgs e)
    {
        DispatcherUnhandledException += Application_DispatcherUnhandledException;
        Exit += App_OnExit;

        await ConfigHandler.Instance.InitializeIfNeededAsync();
        var config = ConfigHandler.Instance.Config;

        _themeService = new ThemeService(config);
        _contrastChecker = new ContrastCheckerService(config);
        _contrastChecker.LoadColorsAndCheckContrast();

        if (config.ContextMenuToggle)
        {
            var program = new Program();
            await program.Main(e.Args);
            await Dispatcher.InvokeAsync(InitializeComponent);
        }
        else
        {
            await RegistryHelper.DeleteContextMenuEntry();
            InitializeComponent();
        }
    }
}