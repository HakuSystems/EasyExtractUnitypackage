namespace EasyExtractCrossPlatform.Services;

/// <summary>
///     Provides context menu integration across supported desktop platforms.
/// </summary>
public static partial class ContextMenuIntegrationService
{
    private const string MenuText = "Extract with EasyExtract";
    private const string MenuKeyName = "EasyExtract";
    private const string CommandArgument = "--extract";
    private const string LinuxDesktopEntryFileName = "easyextract-unitypackage.desktop";
    private const string LinuxActionFileName = "easyextract.desktop";
    private const string LinuxNemoActionFileName = "easyextract.nemo_action";
    private const string LinuxMimeFileName = "easyextract-unitypackage.xml";
    private const string LinuxIconFileName = "easyextract.png";

    public static void UpdateContextMenuIntegration(bool enable)
    {
        var platform = OperatingSystem.IsWindows()
            ? "Windows"
            : OperatingSystem.IsLinux()
                ? "Linux"
                : OperatingSystem.IsMacOS()
                    ? "macOS"
                    : "Unsupported";

        LoggingService.LogInformation($"Updating context menu integration (enable={enable}) on {platform}.");

        try
        {
            if (OperatingSystem.IsWindows())
                UpdateWindowsIntegration(enable);
            else if (OperatingSystem.IsLinux())
                UpdateLinuxIntegration(enable);
            else if (OperatingSystem.IsMacOS())
                UpdateMacIntegration(enable);

            LoggingService.LogInformation($"Context menu integration update completed on {platform}.");
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Context menu integration update failed.", ex);
        }
    }

    private static string? ResolveExecutablePath()
    {
        if (!string.IsNullOrWhiteSpace(Environment.ProcessPath) && File.Exists(Environment.ProcessPath))
            return Environment.ProcessPath;

        try
        {
            var modulePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrWhiteSpace(modulePath) && File.Exists(modulePath))
                return modulePath;
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Failed to resolve executable path for context menu integration.", ex);
        }

        return null;
    }
}