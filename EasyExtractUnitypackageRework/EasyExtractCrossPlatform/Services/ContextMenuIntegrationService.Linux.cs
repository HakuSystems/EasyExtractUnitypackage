using System.Text;
using Avalonia.Platform;

namespace EasyExtractCrossPlatform.Services;

public static partial class ContextMenuIntegrationService
{
    private static void UpdateLinuxIntegration(bool enable)
    {
        LoggingService.LogInformation($"Applying Linux context menu integration (enable={enable}).");

        if (enable)
            TryRegisterLinuxIntegration();
        else
            TryRemoveLinuxIntegration();

        LoggingService.LogInformation("Linux context menu integration change complete.");
    }

    private static void TryRegisterLinuxIntegration()
    {
        var executablePath = ResolveExecutablePath();
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            LoggingService.LogError(
                "Unable to register Linux context menu because the executable path could not be resolved.");
            return;
        }

        var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
        if (string.IsNullOrWhiteSpace(homeDirectory))
        {
            LoggingService.LogError(
                "Unable to register Linux context menu because the user's home directory could not be resolved.");
            return;
        }

        LoggingService.LogInformation(
            $"Registering Linux context menu integration using executable '{executablePath}'.");

        try
        {
            var xdgDataHome = GetXdgDataHome(homeDirectory);
            var iconReference = EnsureLinuxIconInstalled(homeDirectory, xdgDataHome);
            RegisterLinuxDesktopEntry(homeDirectory, xdgDataHome, executablePath, iconReference);
            RegisterLinuxFileManagerAction(homeDirectory, xdgDataHome, executablePath, iconReference);
            RegisterLinuxNemoAction(homeDirectory, xdgDataHome, executablePath, iconReference);
            RegisterLinuxMimeType(homeDirectory, xdgDataHome);
            RefreshLinuxCaches(xdgDataHome);
            LoggingService.LogInformation("Linux context menu integration registered successfully.");
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Failed to register Linux context menu integration.", ex);
        }
    }

    private static void RegisterLinuxDesktopEntry(string homeDirectory, string xdgDataHome, string executablePath,
        string iconReference)
    {
        var applicationsDirectory = Path.Combine(xdgDataHome, "applications");
        Directory.CreateDirectory(applicationsDirectory);

        var desktopFilePath = Path.Combine(applicationsDirectory, LinuxDesktopEntryFileName);
        var desktopContent = new StringBuilder()
            .AppendLine("[Desktop Entry]")
            .AppendLine("Type=Application")
            .AppendLine($"Name={MenuText}")
            .AppendLine("Categories=Utility;Archiving;")
            .AppendLine($"Exec=\"{executablePath}\" {CommandArgument} %F")
            .AppendLine("NoDisplay=false")
            .AppendLine("Terminal=false")
            .AppendLine($"Icon={iconReference}")
            .AppendLine("MimeType=application/x-unitypackage;")
            .ToString();

        File.WriteAllText(desktopFilePath, desktopContent);
        EnsureLinuxFileMode(desktopFilePath,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
            UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead);
    }

    private static void RegisterLinuxFileManagerAction(string homeDirectory, string xdgDataHome, string executablePath,
        string iconReference)
    {
        var actionsDirectory = Path.Combine(xdgDataHome, "file-manager", "actions");
        Directory.CreateDirectory(actionsDirectory);

        var actionFilePath = Path.Combine(actionsDirectory, LinuxActionFileName);
        var actionContent = new StringBuilder()
            .AppendLine("[Desktop Entry]")
            .AppendLine("Type=Action")
            .AppendLine($"Name={MenuText}")
            .AppendLine($"Icon={iconReference}")
            .AppendLine("Profiles=unitypackage;")
            .AppendLine()
            .AppendLine("[X-Action-Profile unitypackage]")
            .AppendLine("MimeTypes=application/x-unitypackage;")
            .AppendLine($"Exec=\"{executablePath}\" {CommandArgument} %F")
            .ToString();

        File.WriteAllText(actionFilePath, actionContent);
        EnsureLinuxFileMode(actionFilePath,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
            UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead);
    }

    private static void RegisterLinuxNemoAction(string homeDirectory, string xdgDataHome, string executablePath,
        string iconReference)
    {
        var nemoDirectory = Path.Combine(xdgDataHome, "nemo", "actions");
        Directory.CreateDirectory(nemoDirectory);

        var nemoFilePath = Path.Combine(nemoDirectory, LinuxNemoActionFileName);
        var nemoContent = new StringBuilder()
            .AppendLine("[Nemo Action]")
            .AppendLine($"Name={MenuText}")
            .AppendLine("Comment=Extract Unity packages with EasyExtract")
            .AppendLine($"Exec=\"{executablePath}\" {CommandArgument} \"%F\"")
            .AppendLine($"Icon-Name={iconReference}")
            .AppendLine("Selection=s")
            .AppendLine("Extensions=unitypackage")
            .AppendLine("EscapeSpaces=true")
            .ToString();

        File.WriteAllText(nemoFilePath, nemoContent);
        EnsureLinuxFileMode(nemoFilePath,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
            UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead);
    }

    private static void RegisterLinuxMimeType(string homeDirectory, string xdgDataHome)
    {
        var mimePackagesDirectory = Path.Combine(xdgDataHome, "mime", "packages");
        Directory.CreateDirectory(mimePackagesDirectory);

        var mimeFilePath = Path.Combine(mimePackagesDirectory, LinuxMimeFileName);
        var mimeContent = """
                          <?xml version="1.0" encoding="UTF-8"?>
                          <mime-info xmlns="http://www.freedesktop.org/standards/shared-mime-info">
                              <mime-type type="application/x-unitypackage">
                                  <comment>Unity package archive</comment>
                                  <glob pattern="*.unitypackage"/>
                              </mime-type>
                          </mime-info>
                          """;

        File.WriteAllText(mimeFilePath, mimeContent);
        EnsureLinuxFileMode(mimeFilePath,
            UnixFileMode.UserRead | UnixFileMode.UserWrite |
            UnixFileMode.GroupRead | UnixFileMode.OtherRead);
    }

    private static string GetXdgDataHome(string homeDirectory)
    {
        var xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (!string.IsNullOrWhiteSpace(xdgDataHome))
            try
            {
                return Path.GetFullPath(xdgDataHome);
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Failed to resolve XDG_DATA_HOME '{xdgDataHome}'.", ex);
            }

        return Path.Combine(homeDirectory, ".local", "share");
    }

    private static string EnsureLinuxIconInstalled(string homeDirectory, string xdgDataHome)
    {
        try
        {
            var iconsDirectory = Path.Combine(xdgDataHome, "icons", "hicolor", "256x256", "apps");
            Directory.CreateDirectory(iconsDirectory);

            var iconPath = Path.Combine(iconsDirectory, LinuxIconFileName);
            using var iconStream = TryLoadIconAsset();
            if (iconStream is null)
                return "utilities-archive";

            using (var fileStream = File.Create(iconPath))
            {
                iconStream.CopyTo(fileStream);
            }

            EnsureLinuxFileMode(iconPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite |
                UnixFileMode.GroupRead | UnixFileMode.OtherRead);

            return Path.GetFileNameWithoutExtension(iconPath);
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Failed to install Linux icon for context menu integration.", ex);
            return "utilities-archive";
        }
    }

    private static MemoryStream? TryLoadIconAsset()
    {
        try
        {
            var assetUri = new Uri("avares://EasyExtractCrossPlatform/Assets/AppLogo.png");
            if (AssetLoader.Exists(assetUri))
            {
                using var assetStream = AssetLoader.Open(assetUri);
                var buffer = new MemoryStream();
                assetStream.CopyTo(buffer);
                buffer.Position = 0;
                return buffer;
            }
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Failed to load icon via Avalonia asset loader.", ex);
        }

        try
        {
            var baseDirectory = AppContext.BaseDirectory;
            if (!string.IsNullOrWhiteSpace(baseDirectory))
            {
                var diskIconPath = Path.Combine(baseDirectory, "Assets", "AppLogo.png");
                if (File.Exists(diskIconPath))
                    return new MemoryStream(File.ReadAllBytes(diskIconPath));
            }
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Failed to load icon from disk.", ex);
        }

        return null;
    }

    private static void RefreshLinuxCaches(string xdgDataHome)
    {
        try
        {
            var applicationsDirectory = Path.Combine(xdgDataHome, "applications");
            if (Directory.Exists(applicationsDirectory))
                TryStartProcess("update-desktop-database", QuoteArgument(applicationsDirectory), applicationsDirectory);

            var mimeDirectory = Path.Combine(xdgDataHome, "mime");
            if (Directory.Exists(mimeDirectory))
                TryStartProcess("update-mime-database", QuoteArgument(mimeDirectory), mimeDirectory);

            var iconsDirectory = Path.Combine(xdgDataHome, "icons", "hicolor");
            if (Directory.Exists(iconsDirectory))
                TryStartProcess("gtk-update-icon-cache", $"-f {QuoteArgument(iconsDirectory)}", iconsDirectory);
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Failed to refresh Linux desktop caches.", ex);
        }
    }

    private static string QuoteArgument(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return $"\"{value}\"";
    }

    private static void EnsureLinuxFileMode(string path, UnixFileMode mode)
    {
        if (!OperatingSystem.IsLinux())
            return;

        try
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                File.SetUnixFileMode(path, mode);
        }
        catch (Exception ex)
        {
            LoggingService.LogError($"Failed to set file mode on '{path}'.", ex);
        }
    }

    private static void TryRemoveLinuxIntegration()
    {
        var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
        if (string.IsNullOrWhiteSpace(homeDirectory))
            return;

        LoggingService.LogInformation("Removing Linux context menu integration.");

        var xdgDataHome = GetXdgDataHome(homeDirectory);
        var desktopFilePath = Path.Combine(xdgDataHome, "applications", LinuxDesktopEntryFileName);
        var actionFilePath = Path.Combine(xdgDataHome, "file-manager", "actions", LinuxActionFileName);
        var nemoFilePath = Path.Combine(xdgDataHome, "nemo", "actions", LinuxNemoActionFileName);
        var mimeFilePath = Path.Combine(xdgDataHome, "mime", "packages", LinuxMimeFileName);
        var iconPath = Path.Combine(xdgDataHome, "icons", "hicolor", "256x256", "apps", LinuxIconFileName);

        DeleteFileIfExists(desktopFilePath);
        DeleteFileIfExists(actionFilePath);
        DeleteFileIfExists(nemoFilePath);
        DeleteFileIfExists(mimeFilePath);
        DeleteFileIfExists(iconPath);

        RefreshLinuxCaches(xdgDataHome);
        LoggingService.LogInformation("Linux context menu integration removed.");
    }
}