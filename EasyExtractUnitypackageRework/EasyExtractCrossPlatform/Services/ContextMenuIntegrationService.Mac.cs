using System.Security;

namespace EasyExtractCrossPlatform.Services;

public static partial class ContextMenuIntegrationService
{
    private static void UpdateMacIntegration(bool enable)
    {
        LoggingService.LogInformation($"Applying macOS context menu integration (enable={enable}).");

        if (enable)
            TryRegisterMacIntegration();
        else
            TryRemoveMacIntegration();

        LoggingService.LogInformation("macOS context menu integration change complete.");
    }

    private static void TryRegisterMacIntegration()
    {
        var executablePath = ResolveExecutablePath();
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            LoggingService.LogError(
                "Unable to register macOS service because the executable path could not be resolved.");
            return;
        }

        var homeDirectory = ResolveMacHomeDirectory();
        if (string.IsNullOrWhiteSpace(homeDirectory))
        {
            LoggingService.LogError(
                "Unable to register macOS service because the user's home directory could not be resolved.");
            return;
        }

        LoggingService.LogInformation($"Registering macOS Automator service using executable '{executablePath}'.");

        try
        {
	        var servicesDirectory = Path.Combine(homeDirectory, "Library", "Services");
	        Directory.CreateDirectory(servicesDirectory);

	        var workflowDirectory = Path.Combine(servicesDirectory, "Extract with EasyExtract.workflow");
	        var contentsDirectory = Path.Combine(workflowDirectory, "Contents");
	        Directory.CreateDirectory(contentsDirectory);

	        var infoPlistPath = Path.Combine(contentsDirectory, "Info.plist");
	        var workflowDocumentPath = Path.Combine(contentsDirectory, "document.wflow");

	        // Attempt to delete existing files to reset permissions if needed
	        if (File.Exists(infoPlistPath)) File.Delete(infoPlistPath);
	        if (File.Exists(workflowDocumentPath)) File.Delete(workflowDocumentPath);

	        File.WriteAllText(infoPlistPath, BuildMacInfoPlist());
	        File.WriteAllText(workflowDocumentPath, BuildMacWorkflowDocument(executablePath));

	        TryStartProcess("/System/Library/CoreServices/pbs", "-flush");
	        LoggingService.LogInformation("macOS service registration completed.");
        }
        catch (Exception ex)
        {
	        LoggingService.LogError("Failed to register macOS service integration.", ex);
        }
    }

    private static void TryRemoveMacIntegration()
    {
	    var homeDirectory = ResolveMacHomeDirectory();

        if (string.IsNullOrWhiteSpace(homeDirectory))
            return;

        LoggingService.LogInformation("Removing macOS Automator service integration.");

        try
        {
	        var workflowDirectory =
		        Path.Combine(homeDirectory, "Library", "Services", "Extract with EasyExtract.workflow");
	        DeleteDirectoryIfExists(workflowDirectory);

	        TryStartProcess("/System/Library/CoreServices/pbs", "-flush");
	        LoggingService.LogInformation("macOS Automator service removed.");
        }
        catch (Exception ex)
        {
	        LoggingService.LogError("Failed to remove macOS service integration.", ex);
        }
    }

    private static string? ResolveMacHomeDirectory()
    {
	    var candidates = new[]
	    {
		    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
		    Environment.GetFolderPath(Environment.SpecialFolder.Personal),
		    Environment.GetEnvironmentVariable("HOME"),
		    Environment.GetEnvironmentVariable("USERPROFILE"),
		    BuildUserProfileFromUserName()
	    };

	    foreach (var candidate in candidates)
	    {
		    if (string.IsNullOrWhiteSpace(candidate))
			    continue;

		    var trimmed = candidate.Trim();
		    if (Directory.Exists(trimmed))
			    return trimmed;
	    }

	    return null;
    }

    private static string? BuildUserProfileFromUserName()
    {
	    var userName = Environment.UserName;
	    if (string.IsNullOrWhiteSpace(userName))
		    return null;

	    return Path.Combine("/Users", userName);
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
}