using System.Runtime.Versioning;
using Microsoft.Win32;

namespace EasyExtractCrossPlatform.Services;

public static partial class ContextMenuIntegrationService
{
    [SupportedOSPlatform("windows")]
    private static void UpdateWindowsIntegration(bool enable)
    {
        LoggingService.LogInformation($"Applying Windows context menu integration (enable={enable}).");

        if (enable)
            TryRegisterWindowsContextMenu();
        else
            TryRemoveWindowsContextMenu();

        LoggingService.LogInformation("Windows context menu integration change complete.");
    }

    [SupportedOSPlatform("windows")]
    private static void TryRegisterWindowsContextMenu()
    {
        var executablePath = ResolveExecutablePath();
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            LoggingService.LogError(
                "Unable to register Windows context menu because the executable path could not be resolved.");
            return;
        }

        LoggingService.LogInformation($"Registering Windows context menu using executable '{executablePath}'.");

        var command = $"\"{executablePath}\" {CommandArgument} \"%1\"";

        RegisterWindowsVerb(@"Software\Classes\.unitypackage", command, executablePath);
        RegisterWindowsVerb(@"Software\Classes\SystemFileAssociations\.unitypackage", command, executablePath);

        LoggingService.LogInformation("Windows context menu registration completed.");
    }

    [SupportedOSPlatform("windows")]
    private static void RegisterWindowsVerb(string baseSubKey, string command, string executablePath)
    {
        try
        {
            using var baseKey = Registry.CurrentUser.CreateSubKey(baseSubKey, true);
            if (baseKey is null)
                return;

            using var shellKey = baseKey.CreateSubKey("shell", true);
            if (shellKey is null)
                return;

            using var entryKey = shellKey.CreateSubKey(MenuKeyName, true);
            if (entryKey is null)
                return;

            var iconResource = GetWindowsIconResource(executablePath);

            entryKey.SetValue(null, MenuText);
            entryKey.SetValue("MUIVerb", MenuText);
            entryKey.SetValue("Icon", iconResource);
            entryKey.SetValue("IconResource", iconResource);
            entryKey.SetValue("Position", "Top");

            using var commandKey = entryKey.CreateSubKey("command", true);
            commandKey?.SetValue(null, command);

            LoggingService.LogInformation($"Registered Windows context menu verb under '{baseSubKey}'.");
        }
        catch (Exception ex)
        {
            LoggingService.LogError($"Failed to register Windows context menu verb at '{baseSubKey}'.", ex);
        }
    }

    [SupportedOSPlatform("windows")]
    private static void TryRemoveWindowsContextMenu()
    {
        LoggingService.LogInformation("Removing Windows context menu integration.");
        RemoveWindowsVerb(@"Software\Classes\.unitypackage");
        RemoveWindowsVerb(@"Software\Classes\SystemFileAssociations\.unitypackage");
        LoggingService.LogInformation("Windows context menu entries removed.");
    }

    [SupportedOSPlatform("windows")]
    private static void RemoveWindowsVerb(string baseSubKey)
    {
        try
        {
            using var shellKey = Registry.CurrentUser.OpenSubKey($"{baseSubKey}\\shell", true);
            shellKey?.DeleteSubKeyTree(MenuKeyName, false);
            LoggingService.LogInformation($"Removed Windows context menu verb from '{baseSubKey}'.");
        }
        catch (Exception ex)
        {
            LoggingService.LogError($"Failed to remove Windows context menu verb from '{baseSubKey}'.", ex);
        }
    }
}