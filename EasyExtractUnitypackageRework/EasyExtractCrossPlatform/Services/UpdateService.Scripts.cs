using System.Text;

namespace EasyExtractCrossPlatform.Services;

public sealed partial class UpdateService
{
    private static ScriptInfo CreateUpdateScript(RuntimePlatform platform, string scriptsDirectory, string contentRoot,
        string installDirectory, string executableRelativePath, Version targetVersion, string applicationName)
    {
        return platform switch
        {
            RuntimePlatform.Windows => CreateWindowsScript(scriptsDirectory, contentRoot, installDirectory,
                executableRelativePath, targetVersion, applicationName),
            RuntimePlatform.MacOS or RuntimePlatform.Linux => CreateUnixScript(scriptsDirectory, contentRoot,
                installDirectory, executableRelativePath, targetVersion, applicationName),
            _ => throw new ArgumentOutOfRangeException(nameof(platform), platform, "Unsupported platform.")
        };
    }

    private static ScriptInfo CreateWindowsScript(string scriptsDirectory, string contentRoot, string installDirectory,
        string executableRelativePath, Version targetVersion, string applicationName)
    {
        var scriptPath = Path.Combine(scriptsDirectory, $"install-{targetVersion}.cmd");
        var builder = new StringBuilder();
        builder.AppendLine("@echo off");
        builder.AppendLine("setlocal");
        builder.AppendLine("set SOURCE=%~1");
        builder.AppendLine("set TARGET=%~2");
        builder.AppendLine("set EXEC_REL=%~3");
        builder.AppendLine("set PID=%~4");
        builder.AppendLine("set APP_NAME=%~5");
        builder.AppendLine($"echo Updating %APP_NAME% to version {targetVersion}...");
        builder.AppendLine(":wait");
        builder.AppendLine("tasklist /FI \"PID eq %PID%\" /FO CSV | findstr /R /C:\",%PID%,\" >NUL");
        builder.AppendLine("if %ERRORLEVEL%==0 (");
        builder.AppendLine("    timeout /t 1 /nobreak >NUL");
        builder.AppendLine("    goto wait");
        builder.AppendLine(")");
        builder.AppendLine("robocopy \"%SOURCE%\" \"%TARGET%\" /MIR /NFL /NDL /NJH /NJS /NC /NS /NP >NUL");
        builder.AppendLine("if errorlevel 8 goto fail");
        builder.AppendLine("set EXEC_PATH=%TARGET%\\%EXEC_REL%");
        builder.AppendLine("if exist \"%EXEC_PATH%\" (");
        builder.AppendLine("    start \"\" \"%EXEC_PATH%\"");
        builder.AppendLine(")");
        builder.AppendLine("exit /b 0");
        builder.AppendLine(":fail");
        builder.AppendLine("echo Update failed while copying files.");
        builder.AppendLine("exit /b 1");

        File.WriteAllText(scriptPath, builder.ToString(), Encoding.UTF8);

        var arguments = new[]
        {
            contentRoot,
            installDirectory,
            executableRelativePath,
            Environment.ProcessId.ToString(),
            applicationName
        };

        return new ScriptInfo(scriptPath, arguments, scriptsDirectory);
    }

    private static ScriptInfo CreateUnixScript(string scriptsDirectory, string contentRoot, string installDirectory,
        string executableRelativePath, Version targetVersion, string applicationName)
    {
        var scriptPath = Path.Combine(scriptsDirectory, $"install-{targetVersion}.sh");
        var builder = new StringBuilder();
        builder.AppendLine("#!/bin/sh");
        builder.AppendLine("SOURCE=\"$1\"");
        builder.AppendLine("TARGET=\"$2\"");
        builder.AppendLine("EXEC_REL=\"$3\"");
        builder.AppendLine("TARGET_PID=\"$4\"");
        builder.AppendLine("APP_NAME=\"$5\"");
        builder.AppendLine($"echo \"Updating $APP_NAME to version {targetVersion}...\"");
        builder.AppendLine("while kill -0 \"$TARGET_PID\" >/dev/null 2>&1; do");
        builder.AppendLine("  sleep 1");
        builder.AppendLine("done");
        builder.AppendLine("mkdir -p \"$TARGET\"");
        builder.AppendLine("if command -v rsync >/dev/null 2>&1; then");
        builder.AppendLine("  rsync -a --delete \"$SOURCE\"/ \"$TARGET\"/");
        builder.AppendLine("else");
        builder.AppendLine("  rm -rf \"$TARGET\"/*");
        builder.AppendLine("  cp -a \"$SOURCE\"/. \"$TARGET\"/");
        builder.AppendLine("fi");
        builder.AppendLine("EXEC_PATH=\"$TARGET/$EXEC_REL\"");
        builder.AppendLine("if [ -d \"$EXEC_PATH\" ] && printf '%s' \"$EXEC_PATH\" | grep -qi '\\.app$'; then");
        builder.AppendLine("  if command -v open >/dev/null 2>&1; then");
        builder.AppendLine("    open \"$EXEC_PATH\" > /dev/null 2>&1 &");
        builder.AppendLine("  else");
        builder.AppendLine("    \"$EXEC_PATH\"/Contents/MacOS/$(basename \"$EXEC_PATH\" .app) > /dev/null 2>&1 &");
        builder.AppendLine("  fi");
        builder.AppendLine("else");
        builder.AppendLine("  chmod +x \"$EXEC_PATH\" >/dev/null 2>&1 || true");
        builder.AppendLine("  \"$EXEC_PATH\" > /dev/null 2>&1 &");
        builder.AppendLine("fi");
        builder.AppendLine("exit 0");

        File.WriteAllText(scriptPath, builder.ToString(), Encoding.UTF8);

        var arguments = new[]
        {
            contentRoot,
            installDirectory,
            executableRelativePath,
            Environment.ProcessId.ToString(),
            applicationName
        };

        return new ScriptInfo(scriptPath, arguments, scriptsDirectory);
    }

    private static void EnsureUnixExecutable(string scriptPath)
    {
        try
        {
            if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
                return;

            const UnixFileMode mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                                      UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                                      UnixFileMode.OtherRead | UnixFileMode.OtherExecute;

            File.SetUnixFileMode(scriptPath, mode);
            LoggingService.LogInformation($"Marked update script '{scriptPath}' as executable.");
        }
        catch (Exception ex)
        {
            LoggingService.LogError($"Failed to set executable bit on '{scriptPath}'.", ex);
        }
    }

    private readonly struct ScriptInfo
    {
        public ScriptInfo(string scriptPath, string[] arguments, string workingDirectory)
        {
            ScriptPath = scriptPath;
            Arguments = arguments;
            WorkingDirectory = workingDirectory;
        }

        public string ScriptPath { get; }
        public string[] Arguments { get; }
        public string WorkingDirectory { get; }
    }
}