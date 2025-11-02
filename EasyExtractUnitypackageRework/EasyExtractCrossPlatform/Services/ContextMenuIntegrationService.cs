using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using System.Security;
using System.Text;
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
            RegisterLinuxDesktopEntry(homeDirectory, executablePath);
            RegisterLinuxFileManagerAction(homeDirectory, executablePath);
            RegisterLinuxNemoAction(homeDirectory, executablePath);
            RegisterLinuxMimeType(homeDirectory);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to register Linux context menu integration: {ex}");
        }
    }

    private static void RegisterLinuxDesktopEntry(string homeDirectory, string executablePath)
    {
        var applicationsDirectory = Path.Combine(homeDirectory, ".local", "share", "applications");
        Directory.CreateDirectory(applicationsDirectory);

        var desktopFilePath = Path.Combine(applicationsDirectory, "easyextract-unitypackage.desktop");
        var desktopContent = new StringBuilder()
            .AppendLine("[Desktop Entry]")
            .AppendLine("Type=Application")
            .AppendLine($"Name={MenuText}")
            .AppendLine("Categories=Utility;Archiving;")
            .AppendLine($"Exec=\"{executablePath}\" {CommandArgument} %F")
            .AppendLine("NoDisplay=false")
            .AppendLine("Terminal=false")
            .AppendLine("Icon=utilities-archive")
            .AppendLine("MimeType=application/x-unitypackage;")
            .ToString();

        File.WriteAllText(desktopFilePath, desktopContent);

        TryStartProcess("update-desktop-database", applicationsDirectory, applicationsDirectory);
    }

    private static void RegisterLinuxFileManagerAction(string homeDirectory, string executablePath)
    {
        var actionsDirectory = Path.Combine(homeDirectory, ".local", "share", "file-manager", "actions");
        Directory.CreateDirectory(actionsDirectory);

        var actionFilePath = Path.Combine(actionsDirectory, "easyextract.desktop");
        var actionContent = new StringBuilder()
            .AppendLine("[Desktop Entry]")
            .AppendLine("Type=Action")
            .AppendLine($"Name={MenuText}")
            .AppendLine("Icon=utilities-archive")
            .AppendLine("Profiles=unitypackage;")
            .AppendLine()
            .AppendLine("[X-Action-Profile unitypackage]")
            .AppendLine("MimeTypes=application/x-unitypackage;")
            .AppendLine($"Exec=\"{executablePath}\" {CommandArgument} %F")
            .ToString();

        File.WriteAllText(actionFilePath, actionContent);
    }

    private static void RegisterLinuxNemoAction(string homeDirectory, string executablePath)
    {
        var nemoDirectory = Path.Combine(homeDirectory, ".local", "share", "nemo", "actions");
        Directory.CreateDirectory(nemoDirectory);

        var nemoFilePath = Path.Combine(nemoDirectory, "easyextract.nemo_action");
        var nemoContent = new StringBuilder()
            .AppendLine("[Nemo Action]")
            .AppendLine($"Name={MenuText}")
            .AppendLine("Comment=Extract Unity packages with EasyExtract")
            .AppendLine($"Exec=\"{executablePath}\" {CommandArgument} \"%F\"")
            .AppendLine("Icon-Name=utilities-archive")
            .AppendLine("Selection=s")
            .AppendLine("Extensions=unitypackage")
            .AppendLine("EscapeSpaces=true")
            .ToString();

        File.WriteAllText(nemoFilePath, nemoContent);
    }

    private static void RegisterLinuxMimeType(string homeDirectory)
    {
        var mimePackagesDirectory = Path.Combine(homeDirectory, ".local", "share", "mime", "packages");
        Directory.CreateDirectory(mimePackagesDirectory);

        var mimeFilePath = Path.Combine(mimePackagesDirectory, "easyextract-unitypackage.xml");
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

        var mimeDirectory = Path.Combine(homeDirectory, ".local", "share", "mime");
        TryStartProcess("update-mime-database", mimeDirectory, mimeDirectory);
    }

    private static void TryRemoveLinuxIntegration()
    {
        var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
        if (string.IsNullOrWhiteSpace(homeDirectory))
            return;

        var desktopFilePath = Path.Combine(homeDirectory, ".local", "share", "applications",
            "easyextract-unitypackage.desktop");
        var actionFilePath = Path.Combine(homeDirectory, ".local", "share", "file-manager", "actions",
            "easyextract.desktop");
        var nemoFilePath = Path.Combine(homeDirectory, ".local", "share", "nemo", "actions", "easyextract.nemo_action");
        var mimeFilePath = Path.Combine(homeDirectory, ".local", "share", "mime", "packages",
            "easyextract-unitypackage.xml");

        DeleteFileIfExists(desktopFilePath);
        DeleteFileIfExists(actionFilePath);
        DeleteFileIfExists(nemoFilePath);
        DeleteFileIfExists(mimeFilePath);

        var applicationsDirectory = Path.Combine(homeDirectory, ".local", "share", "applications");
        var mimeDirectory = Path.Combine(homeDirectory, ".local", "share", "mime");

        TryStartProcess("update-desktop-database", applicationsDirectory, applicationsDirectory);
        TryStartProcess("update-mime-database", mimeDirectory, mimeDirectory);
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