using System.Security;
using EasyExtract.Config.Models;
using EasyExtract.Utilities;

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
                    await BetterLogger.LogAsync("Context menu entry deleted", Importance.Info);
                }
                else
                {
                    await BetterLogger.LogAsync("Context menu entry not found", Importance.Info);
                }
            }
        }
        catch (Exception ex) when (ex is ArgumentException || ex is UnauthorizedAccessException ||
                                   ex is SecurityException || ex is InvalidOperationException)
        {
            await BetterLogger.LogAsync($"Registry deletion error: {ex.Message} - Path: {ContextMenuPath}",
                Importance.Error);
        }
        catch (Exception ex)
        {
            await BetterLogger.LogAsync($"Unexpected error deleting registry key: {ex.Message}", Importance.Error);
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

            await BetterLogger.LogAsync("Context menu entry registered", Importance.Info);

            using (var commandKey = Registry.ClassesRoot.CreateSubKey($"{ContextMenuPath}\\command"))
            {
                if (commandKey == null)
                    throw new InvalidOperationException("Failed to create registry key for command.");

                var assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version;
                var versionComparison = assemblyVersion.CompareTo(new Version("2.0.6.3"));

                var command = versionComparison > 0
                    ? $"\"{Assembly.GetExecutingAssembly().Location.Replace(".dll", ".exe")}\" {CommandSuffix} \"%1\""
                    : $"\"{Assembly.GetExecutingAssembly().Location}\" {CommandSuffix} \"%1\"";

                commandKey.SetValue("", command);
            }

            await BetterLogger.LogAsync("Context menu command registered", Importance.Info);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException || ex is SecurityException ||
                                   ex is InvalidOperationException)
        {
            await BetterLogger.LogAsync($"Registry operation error: {ex.Message}", Importance.Error);
        }
        catch (Exception ex)
        {
            await BetterLogger.LogAsync($"Unexpected error registering context menu: {ex.Message}", Importance.Error);
        }
    }
}