using EasyExtract.Config;
using EasyExtract.Models;
using EasyExtract.Services;
using EasyExtract.Utilities;

namespace EasyExtract.Core;

/// <summary>
///     Interaction logic for App.xaml
/// </summary>
public partial class App
{
    private readonly ConfigHelper _configHelper = new();

    private static async void Application_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        await BetterLogger.LogAsync(e.Exception.Message, "App.xaml.cs", Importance.Error);
        // we cant show a Dialog here because the current doesn't have any active Window yet
        e.Handled = true;
    }

    private async void App_OnExit(object sender, ExitEventArgs e)
    {
        DiscordRpcManager.Instance.Dispose();
        await BetterLogger.LogAsync("Application exited", "App.xaml.cs", Importance.Info);
        base.OnExit(e);
    }

    private async void App_OnStartup(object sender, StartupEventArgs e)
    {
        DispatcherUnhandledException += Application_DispatcherUnhandledException;
        Exit += App_OnExit;

        if (_configHelper.Config.ContextMenuToggle)
        {
            // Run the program logic directly
            var program = new Program();
            await program.Run(e.Args);
        }
        else
        {
            await Program.DeleteContextMenu();
            InitializeComponent();
        }
    }
}