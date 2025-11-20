using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using EasyExtractCrossPlatform.Utilities;

namespace EasyExtractCrossPlatform.Services;

public interface INotificationService
{
    void ShowExtractionSuccess(string packagePath, string outputDirectory, int assetsExtracted);
}

public sealed class NotificationService : INotificationService
{
    private const string DefaultTitle = "Extraction complete";

    public void ShowExtractionSuccess(string packagePath, string outputDirectory, int assetsExtracted)
    {
        var title = DefaultTitle;
        var message = NotificationMessageFormatter.BuildExtractionMessage(packagePath, outputDirectory, assetsExtracted);

        if (OperatingSystem.IsWindows() && TryShowWindowsNotification(title, message))
            return;

        if (OperatingSystem.IsMacOS() && TryShowMacNotification(title, message))
            return;

        if (OperatingSystem.IsLinux() && TryShowLinuxNotification(title, message))
            return;

        LoggingService.LogInformation("System notifications are not available on this platform.");
    }

    private static bool TryShowWindowsNotification(string title, string message)
    {
        try
        {
            var script = BuildWindowsToastScript(title, message);
            var encodedScript = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
            var startInfo = new ProcessStartInfo("powershell.exe")
            {
                UseShellExecute = false,
                CreateNoWindow = true
            };

            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-NonInteractive");
            startInfo.ArgumentList.Add("-EncodedCommand");
            startInfo.ArgumentList.Add(encodedScript);

            using var _ = Process.Start(startInfo);
            return true;
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Failed to trigger Windows notification.", ex);
            return false;
        }
    }

    private static bool TryShowLinuxNotification(string title, string message)
    {
        try
        {
            var startInfo = new ProcessStartInfo("notify-send")
            {
                UseShellExecute = false,
                CreateNoWindow = true
            };

            startInfo.ArgumentList.Add(title);
            startInfo.ArgumentList.Add(message);
            startInfo.ArgumentList.Add("--app-name");
            startInfo.ArgumentList.Add(OperatingSystemInfo.GetApplicationName());

            using var _ = Process.Start(startInfo);
            return true;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 2)
        {
            LoggingService.LogInformation("notify-send was not found on this system. Skipping Linux notification.");
            return false;
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Failed to trigger Linux notification.", ex);
            return false;
        }
    }

    private static bool TryShowMacNotification(string title, string message)
    {
        try
        {
            var startInfo = new ProcessStartInfo("osascript")
            {
                UseShellExecute = false,
                CreateNoWindow = true
            };

            startInfo.ArgumentList.Add("-e");
            startInfo.ArgumentList.Add(BuildAppleScriptCommand(title, message));

            using var _ = Process.Start(startInfo);
            return true;
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Failed to trigger macOS notification.", ex);
            return false;
        }
    }

    private static string BuildWindowsToastScript(string title, string message)
    {
        var safeTitle = EscapeForPowerShell(title);
        var safeMessage = EscapeForPowerShell(message);
        var appId = EscapeForPowerShell(OperatingSystemInfo.GetApplicationName());

        return $@"
$title = '{safeTitle}'
$message = '{safeMessage}'
$appId = '{appId}'
[Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime] | Out-Null
$template = [Windows.UI.Notifications.ToastTemplateType]::ToastText02
$toastXml = [Windows.UI.Notifications.ToastNotificationManager]::GetTemplateContent($template)
$textNodes = $toastXml.GetElementsByTagName('text')
$textNodes.Item(0).AppendChild($toastXml.CreateTextNode($title)) | Out-Null
$textNodes.Item(1).AppendChild($toastXml.CreateTextNode($message)) | Out-Null
$toast = [Windows.UI.Notifications.ToastNotification]::new($toastXml)
$notifier = [Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier($appId)
$notifier.Show($toast)
";
    }

    private static string BuildAppleScriptCommand(string title, string message)
    {
        var safeTitle = EscapeForAppleScript(title);
        var safeMessage = EscapeForAppleScript(message);
        var appName = EscapeForAppleScript(OperatingSystemInfo.GetApplicationName());
        return $"display notification \"{safeMessage}\" with title \"{safeTitle}\" subtitle \"{appName}\"";
    }

    private static string EscapeForPowerShell(string value)
    {
        return string.IsNullOrEmpty(value) ? string.Empty : value.Replace("'", "''");
    }

    private static string EscapeForAppleScript(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (character == '\\' || character == '"')
                builder.Append('\\');

            if (character == '\r' || character == '\n')
            {
                builder.Append(' ');
                continue;
            }

            builder.Append(character);
        }

        return builder.ToString();
    }
}
