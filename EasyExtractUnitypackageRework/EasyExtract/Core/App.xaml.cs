using System.Security.Principal;
using EasyExtract.Config;
using EasyExtract.Services;
using EasyExtract.Utilities.Logger;
using EasyExtract.Views;

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
                    BetterLogger.LogWithContext("Extract argument detected",
                        new Dictionary<string, object> { ["Path"] = path });
                    var extractionHandler = new ExtractionHandler();
                    await extractionHandler.ExtractUnitypackageFromContextMenu(path);
                    Shutdown();
                    return;
                }
            }

            await RegistryHelper.DeleteContextMenuEntry();
            if (config.ContextMenuToggle)
                await RegistryHelper.RegisterContextMenuEntry();

            config.Update.CurrentVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            var dashboard = new Dashboard(new CancellationTokenSource());
            dashboard.InitializeComponent();
            dashboard.Show();
        }
        catch (Exception exc)
        {
            BetterLogger.Exception(exc);
        }
    }

    private static async void Application_DispatcherUnhandledException(object sender,
        DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            BetterLogger.Exception(e.Exception);
            e.Handled = true;
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("has not yet been initialized"))
                return;
            BetterLogger.Exception(ex);
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
            BetterLogger.LogWithContext("Exiting non-admin instance", new Dictionary<string, object>());
            Current.Shutdown();
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            BetterLogger.LogWithContext("User declined elevation", new Dictionary<string, object>());
            Current.Shutdown();
        }
        catch (Exception ex)
        {
            BetterLogger.Exception(ex, "Error running as admin");
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
                    BetterLogger.LogWithContext("Killing process",
                        new Dictionary<string, object> { ["ProcessId"] = process.Id });
                    process.Kill();
                    await process.WaitForExitAsync();
                }
                catch (Exception ex)
                {
                    BetterLogger.Exception(ex, $"Error killing process {process.Id}");
                }
        }
    }
}