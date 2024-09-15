using System.Windows;
using System.Windows.Threading;
using EasyExtract.Config;
using EasyExtract.Discord;

namespace EasyExtract;

/// <summary>
///     Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private readonly ConfigHelper _configHelper = new();
    private readonly BetterLogger _logger = new();

    private async void Application_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        await _logger.LogAsync(e.Exception.Message, "App.xaml.cs", Importance.Error);
        MessageBox.Show(e.Exception.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private async void App_OnExit(object sender, ExitEventArgs e)
    {
        DiscordRpcManager.Instance.Dispose();
        await _logger.LogAsync("Application exited", "App.xaml.cs", Importance.Info);
        base.OnExit(e);
    }

    private void App_OnStartup(object sender, StartupEventArgs e)
    {
        DispatcherUnhandledException += Application_DispatcherUnhandledException;
        Exit += App_OnExit;

        if (_configHelper.Config.ContextMenuToggle)
        {
            // Run the program logic directly
            var program = new Program();
            program.Run(e.Args);
        }
        else
        {
            var program = new Program();
            program.DeleteContextMenu();
            InitializeComponent();
        }
    }
}