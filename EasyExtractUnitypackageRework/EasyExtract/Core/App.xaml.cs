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
    private ContrastCheckerService _contrastChecker;
    private ThemeService _themeService;

    private async void App_OnStartup(object sender, StartupEventArgs e)
    {
        DispatcherUnhandledException += Application_DispatcherUnhandledException;

        await ConfigHandler.Instance.InitializeIfNeededAsync();
        var config = ConfigHandler.Instance.Config;

        _themeService = new ThemeService(config);
        _contrastChecker = new ContrastCheckerService(config);
        _contrastChecker.LoadColorsAndCheckContrast();

        var requireAdmin = !Debugger.IsAttached &&
                           config.ContextMenuToggle &&
                           !IsRunningAsAdmin() &&
                           !e.Args.Contains("--elevated");

        if (requireAdmin)
        {
            await KillAllProcesses(Process.GetCurrentProcess().ProcessName);
            RunAsAdmin(e.Args);
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
                Shutdown(); // Sauberer Exit
                return;
            }
        }

        await RegistryHelper.DeleteContextMenuEntry();
        if (config.ContextMenuToggle)
            await RegistryHelper.RegisterContextMenuEntry();

        var dashboard = new Dashboard();
        dashboard.InitializeComponent();
        dashboard.Show();
    }

    private static async void Application_DispatcherUnhandledException(object sender,
        DispatcherUnhandledExceptionEventArgs e)
    {
        await BetterLogger.LogAsync(e.Exception.Message, Importance.Error);
        e.Handled = true;
    }

    private static bool IsRunningAsAdmin()
    {
        return new WindowsPrincipal(WindowsIdentity.GetCurrent())
            .IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static void RunAsAdmin(string[] args)
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
            BetterLogger.LogAsync("Exiting non-admin instance", Importance.Info).Wait();
            Current.Shutdown();
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            BetterLogger.LogAsync("User declined elevation. Exiting application.", Importance.Info).Wait();
            Current.Shutdown();
        }
        catch (Exception ex)
        {
            BetterLogger.LogAsync($"Error running as admin: {ex.Message}", Importance.Error).Wait();
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