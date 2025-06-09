using System.Security;
using EasyExtract.Utilities.Logger;

namespace EasyExtract.Core;

public static class RegistryHelper
{
    private const string ContextMenuPath = @"*\shell\ExtractWithEasyExtract";
    private const string MenuText = "Extract with EasyExtract";
    private const string CommandSuffix = "--extract";

    public static async Task DeleteContextMenuEntry()
    {
        try
        {
            using (var key = Registry.ClassesRoot.OpenSubKey(ContextMenuPath, true))
            {
                if (key != null)
                {
                    Registry.ClassesRoot.DeleteSubKeyTree(ContextMenuPath);
                    BetterLogger.LogWithContext("Context menu entry deleted", new Dictionary<string, object>
                    {
                        ["Path"] = ContextMenuPath,
                        ["Operation"] = "Delete",
                        ["Status"] = "Success"
                    }, LogLevel.Info, "Registry");
                }
                else
                {
                    BetterLogger.LogWithContext("Context menu entry not found", new Dictionary<string, object>
                    {
                        ["Path"] = ContextMenuPath,
                        ["Operation"] = "Delete",
                        ["Status"] = "NotFound"
                    }, LogLevel.Info, "Registry");
                }
            }
        }
        catch (Exception ex) when (ex is ArgumentException or UnauthorizedAccessException or SecurityException
                                       or InvalidOperationException)
        {
            BetterLogger.LogWithContext($"Registry deletion error: {ex.Message}", new Dictionary<string, object>
            {
                ["Path"] = ContextMenuPath,
                ["Operation"] = "Delete",
                ["Status"] = "Failed",
                ["ErrorType"] = ex.GetType().Name,
                ["ErrorMessage"] = ex.Message
            }, LogLevel.Error, "Registry");
        }
        catch (Exception ex)
        {
            BetterLogger.Exception(ex, "Unexpected error deleting registry key", "Registry");
        }
    }

    public static async Task RegisterContextMenuEntry()
    {
        try
        {
            using (var menuKey = Registry.ClassesRoot.CreateSubKey(ContextMenuPath))
            {
                if (menuKey == null)
                    throw new InvalidOperationException("Failed to create registry key for context menu.");
                menuKey.SetValue("", MenuText);
                menuKey.SetValue("Icon", Assembly.GetExecutingAssembly().Location);
            }

            BetterLogger.LogWithContext("Context menu entry registered", new Dictionary<string, object>
            {
                ["Path"] = ContextMenuPath,
                ["MenuText"] = MenuText
            }, LogLevel.Info);

            using (var commandKey = Registry.ClassesRoot.CreateSubKey($"{ContextMenuPath}\\command"))
            {
                if (commandKey == null)
                    throw new InvalidOperationException("Failed to create registry key for command.");

                var assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version;
                var versionComparison = assemblyVersion!.CompareTo(new Version("2.0.6.3"));

                var command = versionComparison > 0
                    ? $"\"{Assembly.GetExecutingAssembly().Location.Replace(".dll", ".exe")}\" {CommandSuffix} \"%1\""
                    : $"\"{Assembly.GetExecutingAssembly().Location}\" {CommandSuffix} \"%1\"";

                commandKey.SetValue("", command);
            }

            BetterLogger.LogWithContext("Context menu command registered", new Dictionary<string, object>
            {
                ["Path"] = $"{ContextMenuPath}\\command"
            });
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException || ex is SecurityException ||
                                   ex is InvalidOperationException)
        {
            BetterLogger.Exception(ex, "Registry operation error", "Registry");
        }
        catch (Exception ex)
        {
            BetterLogger.Exception(ex, "Unexpected error registering context menu", "Registry");
        }
    }
}