using System.Security;
using System.Security.Principal;
using EasyExtract.Config;
using EasyExtract.Extraction;
using EasyExtract.Utilities;

namespace EasyExtract.Core;

public class Program
{
    private readonly ConfigHelper _configHelper = new();

    public async Task Run(string[] args)
    {
        try
        {
            await BetterLogger.LogAsync($"Run method invoked with arguments: {string.Join(", ", args)}", $"{nameof(Program)}.cs",
                Importance.Info);

            var requireAdmin = !Debugger.IsAttached && _configHelper.Config.ContextMenuToggle && !IsRunningAsAdmin() &&
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
                    await BetterLogger.LogAsync($"Extract argument detected. Path: {path}", $"{nameof(Program)}.cs", Importance.Info);
                    var extractionHandler = new ExtractionHandler();
                    await extractionHandler.ExtractUnitypackageFromContextMenu(path);
                    return;
                }
            }

            await DeleteContextMenu();
            if (_configHelper.Config.ContextMenuToggle) await RegisterContextMenu();

            var app = new App();
            app.InitializeComponent();
            app.Run();
        }
        catch (UnauthorizedAccessException e)
        {
            await BetterLogger.LogAsync($"An error occurred while processing: {e.Message}", $"{nameof(Program)}.cs",
                Importance.Error);
        }
        catch (Exception e)
        {
            await BetterLogger.LogAsync($"An error occurred: {e.Message}", $"{nameof(Program)}.cs", Importance.Error);
        }
    }

    public static async Task DeleteContextMenu()
    {
        const string contextMenuPath = @"*\shell\ExtractWithEasyExtract";
        if (Registry.ClassesRoot.OpenSubKey(contextMenuPath) == null) return;

        try
        {
            Registry.ClassesRoot.DeleteSubKeyTree(contextMenuPath);
            await BetterLogger.LogAsync("Context menu entry deleted", $"{nameof(Program)}.cs", Importance.Info);
        }
        catch (UnauthorizedAccessException ex)
        {
            await BetterLogger.LogAsync($"Permission error: {ex.Message}", $"{nameof(Program)}.cs", Importance.Error);
        }
        catch (SecurityException ex)
        {
            await BetterLogger.LogAsync($"Security error: {ex.Message}", $"{nameof(Program)}.cs", Importance.Error);
        }
        catch (InvalidOperationException ex)
        {
            await BetterLogger.LogAsync($"Registry operation error: {ex.Message}", $"{nameof(Program)}.cs", Importance.Error);
        }
        catch (Exception ex)
        {
            await BetterLogger.LogAsync($"An unexpected error occurred: {ex.Message}", $"{nameof(Program)}.cs", Importance.Error);
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

            await BetterLogger.LogAsync("Context menu entry registered", $"{nameof(Program)}.cs", Importance.Info);

            // Register the command for the context menu entry
            using (var commandKey = Registry.ClassesRoot.CreateSubKey(contextMenuPath + @"\command"))
            {
                if (commandKey == null)
                    throw new InvalidOperationException("Failed to create or open registry key for command.");
                var command =
                    $"\"{Assembly.GetExecutingAssembly().Location.Replace(".dll", ".exe")}\" {commandSuffix} \"%1\"";
                commandKey.SetValue("", command);
            }

            await BetterLogger.LogAsync("Context menu command registered", $"{nameof(Program)}.cs", Importance.Info);
        }
        catch (UnauthorizedAccessException ex)
        {
            await BetterLogger.LogAsync($"Permission error: {ex.Message}", $"{nameof(Program)}.cs", Importance.Error);
        }
        catch (SecurityException ex)
        {
            await BetterLogger.LogAsync($"Security error: {ex.Message}", $"{nameof(Program)}.cs", Importance.Error);
        }
        catch (InvalidOperationException ex)
        {
            await BetterLogger.LogAsync($"Registry operation error: {ex.Message}", $"{nameof(Program)}.cs", Importance.Error);
        }
        catch (Exception ex)
        {
            await BetterLogger.LogAsync($"An unexpected error occurred: {ex.Message}", $"{nameof(Program)}.cs", Importance.Error);
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
            await BetterLogger.LogAsync($"An error occurred while running as admin: {e.Message}", $"{nameof(Program)}.cs",
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
                await BetterLogger.LogAsync($"Killing process {process.Id}", $"{nameof(Program)}.cs", Importance.Info);
                process.Kill();
            }
    }
}