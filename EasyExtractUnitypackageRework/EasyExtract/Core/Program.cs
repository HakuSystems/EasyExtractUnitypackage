using System.Security.Principal;
using EasyExtract.Config;
using EasyExtract.Config.Models;
using EasyExtract.Services;
using EasyExtract.Utilities;

namespace EasyExtract.Core;

public class Program
{
    public async Task Run(string[] args)
    {
        try
        {
            await BetterLogger.LogAsync($"Run method invoked with arguments: {string.Join(", ", args)}",
                Importance.Info);

            var requireAdmin = !Debugger.IsAttached && ConfigHandler.Instance.Config.ContextMenuToggle &&
                               !IsRunningAsAdmin() &&
                               !args.Contains("--elevated");

            if (requireAdmin)
            {
                var assemblyName = Assembly.GetExecutingAssembly().GetName().Name;
                if (!string.IsNullOrEmpty(assemblyName))
                    await KillAllProcesses(assemblyName);
                await RunAsAdmin(args);
                return;
            }

            if (args.Contains("--extract"))
            {
                var extractIndex = Array.IndexOf(args, "--extract");
                var elevatedIndex = Array.IndexOf(args, "--elevated");
                if (elevatedIndex > extractIndex)
                {
                    var path = string.Join(" ",
                        args.Skip(extractIndex + 1).Take(elevatedIndex - extractIndex - 1));
                    await BetterLogger.LogAsync($"Extract argument detected. Path: {path}", Importance.Info);
                    var extractionHandler = new ExtractionHandler();
                    await extractionHandler.ExtractUnitypackageFromContextMenu(path);
                    return;
                }
            }

            await Task.Run(() => RegistryHelper.DeleteContextMenuEntry());
            if (ConfigHandler.Instance.Config.ContextMenuToggle)
                await Task.Run(() => RegistryHelper.RegisterContextMenuEntry());


            if (Application.Current == null)
            {
                var app = new App();
                app.InitializeComponent();
                app.Run();
            }
            else
            {
                await BetterLogger.LogAsync(
                    "Application instance already exists. Skipping creation.",
                    Importance.Info);
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            await BetterLogger.LogAsync($"Access error: {ex.Message}", Importance.Error);
        }
        catch (Exception ex)
        {
            await BetterLogger.LogAsync($"Error: {ex.Message}", Importance.Error);
        }
    }

    private static bool IsRunningAsAdmin()
    {
        return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
    }

    private async Task RunAsAdmin(string[] args)
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
            Environment.Exit(0);
        }
        catch (Win32Exception win32Ex) when (win32Ex.NativeErrorCode == 1223)
        {
            // Error code 1223 means the operation was canceled by the user.
            await BetterLogger.LogAsync("User declined elevation. Exiting application.", Importance.Info);
            Environment.Exit(0);
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
            // Skip the current process
            if (process.Id == Process.GetCurrentProcess().Id)
                continue;
            try
            {
                await BetterLogger.LogAsync($"Killing process {process.Id}", Importance.Info);
                process.Kill();
                await process.WaitForExitAsync(); // Wait for the process to exit completely
            }
            catch (Exception ex)
            {
                await BetterLogger.LogAsync($"Error killing process {process.Id}: {ex.Message}", Importance.Error);
            }
        }
    }
}