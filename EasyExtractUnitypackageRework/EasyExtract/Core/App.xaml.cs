using System.Security.Principal;
using EasyExtract.Config;
using EasyExtract.Services;
using EasyExtract.Utilities;
using EasyExtract.Views;
using Importance = EasyExtract.Config.Models.Importance;

namespace EasyExtract.Core;

/// <summary>
///     Interaction logic for App.xaml
/// </summary>
public partial class App
{
    private async void App_OnStartup(object sender, StartupEventArgs e)
    {
        try
        {
            DispatcherUnhandledException += Application_DispatcherUnhandledException;

            await ConfigHandler.Instance.InitializeIfNeededAsync();
            var config = ConfigHandler.Instance.Config;


            var requireAdmin = !Debugger.IsAttached &&
                               config.ContextMenuToggle &&
                               !IsRunningAsAdmin() &&
                               !e.Args.Contains("--elevated");

            if (requireAdmin)
            {
                await KillAllProcesses(Process.GetCurrentProcess().ProcessName);
                await RunAsAdmin(e.Args);
                return;
            }

            if (e.Args.Contains("--extract"))
            {
                var extractIndex = Array.IndexOf(e.Args, "--extract");
                var elevatedIndex = Array.IndexOf(e.Args, "--elevated");
                if (elevatedIndex > extractIndex)
                {
                    var path = string.Join(" ",
                        e.Args.Skip(extractIndex + 1).Take(elevatedIndex - extractIndex - 1));
                    await BetterLogger.LogAsync($"Extract argument detected. Path: {path}", Importance.Info);
                    var extractionHandler = new ExtractionHandler();
                    await extractionHandler.ExtractUnitypackageFromContextMenu(path);
                    Shutdown();
                    return;
                }
            }

            await RegistryHelper.DeleteContextMenuEntry();
            if (config.ContextMenuToggle)
                await RegistryHelper.RegisterContextMenuEntry();

            var dashboard = new Dashboard(new CancellationTokenSource());
            dashboard.InitializeComponent();
            dashboard.Show();
        }
        catch (Exception exc)
        {
            await BetterLogger.LogAsync(exc.Message, Importance.Error);
        }
    }

    private static async void Application_DispatcherUnhandledException(object sender,
        DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            await BetterLogger.LogAsync(e.Exception.Message, Importance.Error);
            e.Handled = true;
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("has not yet been initialized"))
                return;
            await BetterLogger.LogAsync(ex.Message, Importance.Error);
        }
    }

    private static bool IsRunningAsAdmin()
    {
        return new WindowsPrincipal(WindowsIdentity.GetCurrent())
            .IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static async Task RunAsAdmin(string[] args)
    {
        var processInfo = new ProcessStartInfo
        {
            UseShellExecute = true,
            FileName = Environment.ProcessPath,
            Verb = "runas",
            Arguments = $"{string.Join(" ", args)} --elevated"
        };

        try
        {
            Process.Start(processInfo);
            await BetterLogger.LogAsync("Exiting non-admin instance", Importance.Info);
            Current.Shutdown();
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            await BetterLogger.LogAsync("User declined elevation. Exiting application.", Importance.Info);
            Current.Shutdown();
        }
        catch (Exception ex)
        {
            await BetterLogger.LogAsync($"Error running as admin: {ex.Message}", Importance.Error);
            throw;
        }
    }

    private static async Task KillAllProcesses(string processName)
    {
        foreach (var process in Process.GetProcessesByName(processName))
        {
            if (process.Id == Process.GetCurrentProcess().Id)
                continue;

            if (!process.HasExited)
                try
                {
                    await BetterLogger.LogAsync($"Killing process {process.Id}", Importance.Info);
                    process.Kill();
                    await process.WaitForExitAsync();
                }
                catch (Exception ex)
                {
                    await BetterLogger.LogAsync($"Error killing process {process.Id}: {ex.Message}",
                        Importance.Error);
                }
        }
    }
}