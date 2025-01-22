using System.Security;
using System.Security.Principal;
using EasyExtract.Config;
using EasyExtract.Models;
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
                if (assemblyName != null)
                    await KillAllProcesses(assemblyName);
                await RunAsAdmin(args);
                return;
            }

            // Check if the user wants to extract a unitypackage from the context menu
            if (args.Length > 1 && args.Contains("--extract"))
            {
                var extractIndex = Array.IndexOf(args, "--extract");
                var elevatedIndex = Array.IndexOf(args, "--elevated");

                if (elevatedIndex > extractIndex)
                {
                    var path = string.Join(" ", args.Skip(extractIndex + 1).Take(elevatedIndex - extractIndex - 1));
                    await BetterLogger.LogAsync($"Extract argument detected. Path: {path}", Importance.Info);
                    var extractionHandler = new ExtractionHandler();
                    await extractionHandler.ExtractUnitypackageFromContextMenu(path);
                    return;
                }
            }

            await DeleteContextMenu();
            if (ConfigHandler.Instance.Config.ContextMenuToggle) await RegisterContextMenu();

            var app = new App();
            app.InitializeComponent();
            app.Run();
        }
        catch (UnauthorizedAccessException e)
        {
            await BetterLogger.LogAsync($"An error occurred while processing: {e.Message}",
                Importance.Error);
        }
        catch (Exception e)
        {
            await BetterLogger.LogAsync($"An error occurred: {e.Message}", Importance.Error);
        }
    }

    public static async Task DeleteContextMenu()
    {
        const string contextMenuPath = @"*\shell\ExtractWithEasyExtract";
        if (Registry.ClassesRoot.OpenSubKey(contextMenuPath) == null)
        {
            await BetterLogger.LogAsync("Context menu entry not found", Importance.Info);
            if (Registry.ClassesRoot.OpenSubKey(contextMenuPath) != null)
                try
                {
                    Registry.ClassesRoot.DeleteSubKeyTree(contextMenuPath);
                    await BetterLogger.LogAsync("Context menu entry deleted", Importance.Info);
                }
                catch (ArgumentException ex)
                {
                    await BetterLogger.LogAsync(
                        $"Registry argument error: {ex.Message} - Subkey path: {contextMenuPath}",
                        Importance.Error);
                }
                catch (UnauthorizedAccessException ex)
                {
                    await BetterLogger.LogAsync($"Permission error: {ex.Message}", Importance.Error);
                }
                catch (SecurityException ex)
                {
                    await BetterLogger.LogAsync($"Security error: {ex.Message}", Importance.Error);
                }
                catch (InvalidOperationException ex)
                {
                    await BetterLogger.LogAsync($"Registry operation error: {ex.Message}", Importance.Error);
                }
                catch (Exception ex)
                {
                    await BetterLogger.LogAsync($"An unexpected error occurred: {ex.Message}",
                        Importance.Error);
                }
            else
                await BetterLogger.LogAsync("Context menu entry not found", Importance.Info);

            try
            {
                Registry.ClassesRoot.DeleteSubKeyTree(contextMenuPath);
                await BetterLogger.LogAsync("Context menu entry deleted", Importance.Info);
            }
            catch (ArgumentException ex)
            {
                await BetterLogger.LogAsync($"Registry argument error: {ex.Message} - Subkey path: {contextMenuPath}",
                    Importance.Error);
            }
            catch (UnauthorizedAccessException ex)
            {
                await BetterLogger.LogAsync($"Permission error: {ex.Message}", Importance.Error);
            }
            catch (SecurityException ex)
            {
                await BetterLogger.LogAsync($"Security error: {ex.Message}", Importance.Error);
            }
            catch (InvalidOperationException ex)
            {
                await BetterLogger.LogAsync($"Registry operation error: {ex.Message}", Importance.Error);
            }
            catch (Exception ex)
            {
                await BetterLogger.LogAsync($"An unexpected error occurred: {ex.Message}", Importance.Error);
            }
        }
    }

    private static async Task RegisterContextMenu()
    {
        const string contextMenuPath = @"*\shell\ExtractWithEasyExtract";
        const string menuText = "Extract with EasyExtract";
        const string commandSuffix = "--extract";

        try
        {
            // Register the context menu entry
            using (var menuKey = Registry.ClassesRoot.CreateSubKey(contextMenuPath))
            {
                if (menuKey == null)
                    throw new InvalidOperationException("Failed to create or open registry key for context menu.");
                menuKey.SetValue("", menuText);
                menuKey.SetValue("Icon", Assembly.GetExecutingAssembly().Location);
            }

            await BetterLogger.LogAsync("Context menu entry registered", Importance.Info);

            // Register the command for the context menu entry
            using (var commandKey = Registry.ClassesRoot.CreateSubKey(contextMenuPath + @"\command"))
            {
                if (commandKey == null)
                    throw new InvalidOperationException("Failed to create or open registry key for command.");

                var assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version;
                var versionComparison = assemblyVersion.CompareTo(new Version("2.0.6.3"));

                string command;
                if (versionComparison > 0)
                    command =
                        $"\"{Assembly.GetExecutingAssembly().Location.Replace(".dll", ".exe")}\" {commandSuffix} \"%1\"";
                else
                    // If assembly version is <= 2.0.6.3
                    command = $"\"{Assembly.GetExecutingAssembly().Location}\" {commandSuffix} \"%1\"";

                commandKey.SetValue("", command);
            }

            await BetterLogger.LogAsync("Context menu command registered", Importance.Info);
        }
        catch (UnauthorizedAccessException ex)
        {
            await BetterLogger.LogAsync($"Permission error: {ex.Message}", Importance.Error);
        }
        catch (SecurityException ex)
        {
            await BetterLogger.LogAsync($"Security error: {ex.Message}", Importance.Error);
        }
        catch (InvalidOperationException ex)
        {
            await BetterLogger.LogAsync($"Registry operation error: {ex.Message}", Importance.Error);
        }
        catch (Exception ex)
        {
            await BetterLogger.LogAsync($"An unexpected error occurred: {ex.Message}", Importance.Error);
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
            FileName = Environment.ProcessPath, // Use the current process path
            Verb = "runas",
            Arguments = string.Join(" ", args) + " --elevated"
        };

        try
        {
            Process.Start(processInfo);
            await BetterLogger.LogAsync("Exiting non-admin instance", Importance.Info);
            Environment.Exit(0); // Exit the current (non-admin) instance
        }
        catch (Exception e)
        {
            await BetterLogger.LogAsync($"An error occurred while running as admin: {e.Message}",
                Importance.Error);
            // we cant show a Dialog here because the current doesn't have any active Window yet
            throw;
        }
    }

    private static async Task KillAllProcesses(string processName)
    {
        // Only kill processes with the same name as the current process but not the current process itself
        foreach (var process in Process.GetProcessesByName(processName))
            if (process.Id != Process.GetCurrentProcess().Id)
            {
                await BetterLogger.LogAsync($"Killing process {process.Id}", Importance.Info);
                process.Kill();
            }
    }
}