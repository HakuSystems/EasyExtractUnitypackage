using System.Diagnostics;
using System.Reflection;
using System.Security;
using System.Security.Principal;
using System.Windows;
using EasyExtract.Config;
using EasyExtract.Extraction;
using EasyExtract.Services.CustomMessageBox;
using Microsoft.Win32;

namespace EasyExtract;

public class Program
{
    private readonly ConfigHelper _configHelper = new();
    private readonly BetterLogger _logger = new();

    public async void Run(string[] args)
    {
        try
        {
            await _logger.LogAsync($"Run method invoked with arguments: {string.Join(", ", args)}", "Program.cs",
                Importance.Info);

            var requireAdmin = !Debugger.IsAttached && _configHelper.Config.ContextMenuToggle && !IsRunningAsAdmin() &&
                               !args.Contains("--elevated");

            if (requireAdmin)
            {
                KillAllProcesses(Assembly.GetExecutingAssembly().GetName().Name);
                RunAsAdmin(args);
                return;
            }

            if (args.Length > 1 && args.Contains("--extract"))
            {
                var extractIndex = Array.IndexOf(args, "--extract");
                var elevatedIndex = Array.IndexOf(args, "--elevated");

                if (elevatedIndex > extractIndex)
                {
                    var path = string.Join(" ", args.Skip(extractIndex + 1).Take(elevatedIndex - extractIndex - 1));
                    await _logger.LogAsync($"Extract argument detected. Path: {path}", "Program.cs", Importance.Info);
                    var extractionHandler = new ExtractionHandler();
                    await extractionHandler.ExtractUnitypackageFromContextMenu(path);
                    return;
                }
            }

            DeleteContextMenu();
            if (_configHelper.Config.ContextMenuToggle) RegisterContextMenu();

            var app = new App();
            app.InitializeComponent();
            app.Run();
        }
        catch (UnauthorizedAccessException e)
        {
            await _logger.LogAsync($"An error occurred while processing: {e.Message}", "Program.cs", Importance.Error);
        }
        catch (Exception e)
        {
            await _logger.LogAsync($"An error occurred: {e.Message}", "Program.cs", Importance.Error);
        }
    }

    public async void DeleteContextMenu()
    {
        const string contextMenuPath = @"*\shell\ExtractWithEasyExtract";
        if (Registry.ClassesRoot.OpenSubKey(contextMenuPath) == null) return;

        try
        {
            Registry.ClassesRoot.DeleteSubKeyTree(contextMenuPath);
            await _logger.LogAsync("Context menu entry deleted", "Program.cs", Importance.Info);
        }
        catch (UnauthorizedAccessException ex)
        {
            await _logger.LogAsync($"Permission error: {ex.Message}", "Program.cs", Importance.Error);
        }
        catch (SecurityException ex)
        {
            await _logger.LogAsync($"Security error: {ex.Message}", "Program.cs", Importance.Error);
        }
        catch (InvalidOperationException ex)
        {
            await _logger.LogAsync($"Registry operation error: {ex.Message}", "Program.cs", Importance.Error);
        }
        catch (Exception ex)
        {
            await _logger.LogAsync($"An unexpected error occurred: {ex.Message}", "Program.cs", Importance.Error);
        }
    }

    private async void RegisterContextMenu()
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

            await _logger.LogAsync("Context menu entry registered", "Program.cs", Importance.Info);

            // Register the command for the context menu entry
            using (var commandKey = Registry.ClassesRoot.CreateSubKey(contextMenuPath + @"\command"))
            {
                if (commandKey == null)
                    throw new InvalidOperationException("Failed to create or open registry key for command.");
                var command =
                    $"\"{Assembly.GetExecutingAssembly().Location.Replace(".dll", ".exe")}\" {commandSuffix} \"%1\"";
                commandKey.SetValue("", command);
            }

            await _logger.LogAsync("Context menu command registered", "Program.cs", Importance.Info);
        }
        catch (UnauthorizedAccessException ex)
        {
            await _logger.LogAsync($"Permission error: {ex.Message}", "Program.cs", Importance.Error);
        }
        catch (SecurityException ex)
        {
            await _logger.LogAsync($"Security error: {ex.Message}", "Program.cs", Importance.Error);
        }
        catch (InvalidOperationException ex)
        {
            await _logger.LogAsync($"Registry operation error: {ex.Message}", "Program.cs", Importance.Error);
        }
        catch (Exception ex)
        {
            await _logger.LogAsync($"An unexpected error occurred: {ex.Message}", "Program.cs", Importance.Error);
        }
    }

    private bool IsRunningAsAdmin()
    {
        return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
    }

    private void RunAsAdmin(string[] args)
    {
        var processInfo = new ProcessStartInfo
        {
            UseShellExecute = true,
            FileName = Assembly.GetExecutingAssembly().Location.Replace(".dll", ".exe"),
            Verb = "runas",
            Arguments = string.Join(" ", args) + " --elevated"
        };

        try
        {
            Process.Start(processInfo);
            Environment.Exit(0); // Exit the current (non-admin) instance
        }
        catch (Exception e)
        {
            var customMessageBox =
                new CustomMessageBox(
                    "An error occurred while trying to run as administrator. Please check the logs for more information.",
                    "Error", MessageBoxButton.OK);
            customMessageBox.ShowDialog();
            throw;
        }
    }

    public static async void KillAllProcesses(string easyextract)
    {
        // Only kill processes with the same name as the current process but not the current process itself
        foreach (var process in Process.GetProcessesByName(easyextract))
            if (process.Id != Process.GetCurrentProcess().Id)
            {
                var logger = new BetterLogger();
                await logger.LogAsync($"Killing process {process.Id}", "Program.cs", Importance.Info);
                process.Kill();
            }
    }
}