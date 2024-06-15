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
    private readonly BetterLogger _logger = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += Application_DispatcherUnhandledException;
        Exit += App_OnExit;
    }

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
}