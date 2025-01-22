using EasyExtract.Config;
using EasyExtract.Services;
using EasyExtract.Utilities;
using Importance = EasyExtract.Models.Importance;

namespace EasyExtract.Core;

/// <summary>
///     Interaction logic for App.xaml
/// </summary>
public partial class App
{
    //New Design Related
    private ContrastCheckerService _contrastChecker;
    private ThemeService _themeService;

    private static async void Application_DispatcherUnhandledException(object sender,
        DispatcherUnhandledExceptionEventArgs e)
    {
        await BetterLogger.LogAsync(e.Exception.Message, Importance.Error);
        // we cant show a Dialog here because the current doesn't have any active Window yet
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

        // Initialize config (lazy load if needed)
        await ConfigHandler.Instance.InitializeIfNeededAsync();
        var config = ConfigHandler.Instance.Config;

        // Pass only the config to our services; they will log via BetterLogger
        _themeService = new ThemeService(config);
        _contrastChecker = new ContrastCheckerService(config);
        _contrastChecker.LoadColorsAndCheckContrast();

        // If the user wants the context menu to show, handle the main flow
        if (ConfigHandler.Instance.Config.ContextMenuToggle)
        {
            // Run the program logic directly
            var program = new Program();
            _ = program.Run(e.Args);
        }
        else
        {
            _ = Program.DeleteContextMenu();
            InitializeComponent();
        }
    }
}