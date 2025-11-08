using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using System.Security;
using System.Text;
using Avalonia.Platform;
using Microsoft.Win32;

namespace EasyExtractCrossPlatform.Services;

/// <summary>
///     Provides context menu integration across supported desktop platforms.
/// </summary>
public static class ContextMenuIntegrationService
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
        try
        {
            if (OperatingSystem.IsWindows())
                UpdateWindowsIntegration(enable);
            else if (OperatingSystem.IsLinux())
                UpdateLinuxIntegration(enable);
            else if (OperatingSystem.IsMacOS())
                UpdateMacIntegration(enable);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Context menu integration update failed: {ex}");
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
            Debug.WriteLine($"Failed to resolve executable path: {ex}");
        }

        return null;
    }

    #region Windows

    [SupportedOSPlatform("windows")]
    private static void UpdateWindowsIntegration(bool enable)
    {
        if (enable)
            TryRegisterWindowsContextMenu();
        else
            TryRemoveWindowsContextMenu();
    }

    [SupportedOSPlatform("windows")]
    private static void TryRegisterWindowsContextMenu()
    {
        var executablePath = ResolveExecutablePath();
        if (string.IsNullOrWhiteSpace(executablePath))
            return;

        var command = $"\"{executablePath}\" {CommandArgument} \"%1\"";

        RegisterWindowsVerb(@"Software\Classes\.unitypackage", command, executablePath);
        RegisterWindowsVerb(@"Software\Classes\SystemFileAssociations\.unitypackage", command, executablePath);
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
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to register Windows verb '{baseSubKey}': {ex}");
        }
    }

    [SupportedOSPlatform("windows")]
    private static void TryRemoveWindowsContextMenu()
    {
        RemoveWindowsVerb(@"Software\Classes\.unitypackage");
        RemoveWindowsVerb(@"Software\Classes\SystemFileAssociations\.unitypackage");
    }

    [SupportedOSPlatform("windows")]
    private static void RemoveWindowsVerb(string baseSubKey)
    {
        try
        {
            using var shellKey = Registry.CurrentUser.OpenSubKey($"{baseSubKey}\\shell", true);
            shellKey?.DeleteSubKeyTree(MenuKeyName, false);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to remove Windows verb '{baseSubKey}': {ex}");
        }
    }

    #endregion

    #region Linux

    private static void UpdateLinuxIntegration(bool enable)
    {
        if (enable)
            TryRegisterLinuxIntegration();
        else
            TryRemoveLinuxIntegration();
    }

    private static void TryRegisterLinuxIntegration()
    {
        var executablePath = ResolveExecutablePath();
        if (string.IsNullOrWhiteSpace(executablePath))
            return;

        var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
        if (string.IsNullOrWhiteSpace(homeDirectory))
            return;

        try
        {
            var xdgDataHome = GetXdgDataHome(homeDirectory);
            var iconReference = EnsureLinuxIconInstalled(homeDirectory, xdgDataHome);
            RegisterLinuxDesktopEntry(homeDirectory, xdgDataHome, executablePath, iconReference);
            RegisterLinuxFileManagerAction(homeDirectory, xdgDataHome, executablePath, iconReference);
            RegisterLinuxNemoAction(homeDirectory, xdgDataHome, executablePath, iconReference);
            RegisterLinuxMimeType(homeDirectory, xdgDataHome);
            RefreshLinuxCaches(xdgDataHome);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to register Linux context menu integration: {ex}");
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
                Debug.WriteLine($"Failed to resolve XDG_DATA_HOME '{xdgDataHome}': {ex}");
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
            Debug.WriteLine($"Failed to install Linux icon: {ex}");
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
            Debug.WriteLine($"Failed to load icon via Avalonia asset loader: {ex}");
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
            Debug.WriteLine($"Failed to load icon from disk: {ex}");
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
            Debug.WriteLine($"Failed to refresh Linux caches: {ex}");
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
            Debug.WriteLine($"Failed to set file mode on '{path}': {ex}");
        }
    }

    private static void TryRemoveLinuxIntegration()
    {
        var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
        if (string.IsNullOrWhiteSpace(homeDirectory))
            return;

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
    }

    #endregion

    #region macOS

    private static void UpdateMacIntegration(bool enable)
    {
        if (enable)
            TryRegisterMacIntegration();
        else
            TryRemoveMacIntegration();
    }

    private static void TryRegisterMacIntegration()
    {
        var executablePath = ResolveExecutablePath();
        if (string.IsNullOrWhiteSpace(executablePath))
            return;

        var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
        if (string.IsNullOrWhiteSpace(homeDirectory))
            return;

        var servicesDirectory = Path.Combine(homeDirectory, "Library", "Services");
        Directory.CreateDirectory(servicesDirectory);

        var workflowDirectory = Path.Combine(servicesDirectory, "Extract with EasyExtract.workflow");
        var contentsDirectory = Path.Combine(workflowDirectory, "Contents");
        Directory.CreateDirectory(contentsDirectory);

        var infoPlistPath = Path.Combine(contentsDirectory, "Info.plist");
        var workflowDocumentPath = Path.Combine(contentsDirectory, "document.wflow");

        File.WriteAllText(infoPlistPath, BuildMacInfoPlist());
        File.WriteAllText(workflowDocumentPath, BuildMacWorkflowDocument(executablePath));

        TryStartProcess("/System/Library/CoreServices/pbs", "-flush");
    }

    private static void TryRemoveMacIntegration()
    {
        var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
        if (string.IsNullOrWhiteSpace(homeDirectory))
            return;

        var workflowDirectory = Path.Combine(homeDirectory, "Library", "Services", "Extract with EasyExtract.workflow");
        DeleteDirectoryIfExists(workflowDirectory);

        TryStartProcess("/System/Library/CoreServices/pbs", "-flush");
    }

    private static string BuildMacInfoPlist()
    {
        return """
               <?xml version="1.0" encoding="UTF-8"?>
               <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
               <plist version="1.0">
               <dict>
               	<key>CFBundleIdentifier</key>
               	<string>com.easyextract.workflow.extract</string>
               	<key>CFBundleName</key>
               	<string>Extract with EasyExtract</string>
               	<key>CFBundleShortVersionString</key>
               	<string>1.0</string>
               	<key>CFBundleVersion</key>
               	<string>1.0</string>
               	<key>NSServices</key>
               	<array>
               		<dict>
               			<key>NSMenuItem</key>
               			<dict>
               				<key>default</key>
               				<string>Extract with EasyExtract</string>
               			</dict>
               			<key>NSMessage</key>
               			<string>runWorkflowAsService</string>
               			<key>NSRequiredContext</key>
               			<dict>
               				<key>NSApplicationIdentifier</key>
               				<string>com.apple.finder</string>
               			</dict>
               			<key>NSSendFileTypes</key>
               			<array>
               				<string>public.data</string>
               			</array>
               		</dict>
               	</array>
               </dict>
               </plist>
               """;
    }

    private static string BuildMacWorkflowDocument(string executablePath)
    {
        var commandScript = $$"""
                              for f in "$@"; do
                                if [ -f "$f" ]; then
                                  extension="${f##*.}"
                                  lower="$(printf '%s' "$extension" | tr '[:upper:]' '[:lower:]')"
                                  if [ "$lower" = "unitypackage" ]; then
                                    "{{executablePath}}" {{CommandArgument}} "$f"
                                  fi
                                fi
                              done
                              """;

        var escapedScript = SecurityElement.Escape(commandScript);

        return $"""
                <?xml version="1.0" encoding="UTF-8"?>
                <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
                <plist version="1.0">
                <dict>
                	<key>actions</key>
                	<array>
                		<dict>
                			<key>AMActionVersion</key>
                			<string>2.0</string>
                			<key>AMApplicationBuild</key>
                			<string>523</string>
                			<key>AMApplicationVersion</key>
                			<string>2.10</string>
                			<key>AMParameterProperties</key>
                			<dict>
                				<key>COMMAND_STRING</key>
                				<dict>
                					<key>isPathPopUp</key>
                					<false/>
                				</dict>
                				<key>CheckedForUserDefaultShell</key>
                				<dict>
                					<key>isPathPopUp</key>
                					<false/>
                				</dict>
                				<key>inputMethod</key>
                				<dict>
                					<key>isPathPopUp</key>
                					<true/>
                				</dict>
                				<key>shell</key>
                				<dict>
                					<key>isPathPopUp</key>
                					<true/>
                				</dict>
                				<key>source</key>
                				<dict>
                					<key>isPathPopUp</key>
                					<false/>
                				</dict>
                			</dict>
                			<key>AMProvidesUndo</key>
                			<false/>
                			<key>ActionBundlePath</key>
                			<string>/System/Library/Automator/Run Shell Script.action</string>
                			<key>ActionName</key>
                			<string>Run Shell Script</string>
                			<key>ActionParameters</key>
                			<dict>
                				<key>COMMAND_STRING</key>
                				<string>{escapedScript}</string>
                				<key>CheckedForUserDefaultShell</key>
                				<integer>1</integer>
                				<key>inputMethod</key>
                				<integer>1</integer>
                				<key>shell</key>
                				<string>/bin/bash</string>
                			</dict>
                			<key>isViewVisible</key>
                			<true/>
                			<key>location</key>
                			<string>~/Library/Services</string>
                		</dict>
                	</array>
                	<key>workflowMetaData</key>
                	<dict>
                		<key>serviceInputTypeIdentifier</key>
                		<string>com.apple.Automator.fileSystemObject</string>
                		<key>serviceInfo</key>
                		<dict/>
                		<key>serviceName</key>
                		<string>Extract with EasyExtract</string>
                		<key>serviceOutputTypeIdentifier</key>
                		<string>com.apple.Automator.nothing</string>
                		<key>serviceProcessesInput</key>
                		<integer>0</integer>
                		<key>workflowTypeIdentifier</key>
                		<string>com.apple.Automator.servicesMenu</string>
                	</dict>
                </dict>
                </plist>
                """;
    }

    #endregion

    #region Helpers

    [SupportedOSPlatform("windows")]
    private static string GetWindowsIconResource(string executablePath)
    {
        try
        {
            var baseDirectory = AppContext.BaseDirectory;
            if (!string.IsNullOrWhiteSpace(baseDirectory))
            {
                var iconCandidate = Path.Combine(baseDirectory, "Smallicon.ico");
                if (File.Exists(iconCandidate))
                    return iconCandidate;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to resolve icon candidate: {ex}");
        }

        return $"{executablePath},0";
    }

    private static void TryStartProcess(string fileName, string arguments, string? workingDirectory = null)
    {
        try
        {
            if ((OperatingSystem.IsLinux() || OperatingSystem.IsMacOS()) &&
                !Path.IsPathRooted(fileName) &&
                !IsUnixCommandAvailable(fileName))
            {
                Debug.WriteLine($"Skipping execution of '{fileName}' because it was not found on PATH.");
                return;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory) ? string.Empty : workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            process?.Dispose();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to execute '{fileName} {arguments}': {ex}");
        }
    }

    private static bool IsUnixCommandAvailable(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return false;

        try
        {
            if (Path.IsPathRooted(command))
                return File.Exists(command);

            var pathValue = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrWhiteSpace(pathValue))
                return false;

            var segments = pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
            foreach (var segment in segments)
            {
                var trimmed = segment.Trim();
                if (string.IsNullOrWhiteSpace(trimmed))
                    continue;

                var candidate = Path.Combine(trimmed, command);
                if (File.Exists(candidate))
                    return true;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to probe PATH for '{command}': {ex}");
        }

        return false;
    }

    private static void DeleteFileIfExists(string? path)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to delete file '{path}': {ex}");
        }
    }

    private static void DeleteDirectoryIfExists(string? path)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                Directory.Delete(path, true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to delete directory '{path}': {ex}");
        }
    }

    #endregion
}