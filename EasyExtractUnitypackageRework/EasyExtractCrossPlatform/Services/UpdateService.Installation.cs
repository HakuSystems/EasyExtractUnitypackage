using System;
using System.Diagnostics;

namespace EasyExtractCrossPlatform.Services;

public sealed partial class UpdateService
{
    public bool TryLaunchPreparedUpdate(UpdatePreparation preparation)
    {
        try
        {
            LoggingService.LogInformation(
                $"LaunchUpdateScript: start | script='{preparation.ScriptPath}' | workingDirectory='{preparation.WorkingDirectory}' | arguments='{string.Join(' ', preparation.ScriptArguments)}'");

            ProcessStartInfo startInfo;
            if (OperatingSystem.IsWindows())
            {
                startInfo = new ProcessStartInfo("cmd.exe")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = preparation.WorkingDirectory
                };

                startInfo.ArgumentList.Add("/c");
                startInfo.ArgumentList.Add(preparation.ScriptPath);
            }
            else
            {
                startInfo = new ProcessStartInfo("/bin/sh")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = preparation.WorkingDirectory
                };

                startInfo.ArgumentList.Add(preparation.ScriptPath);
            }

            foreach (var argument in preparation.ScriptArguments)
                startInfo.ArgumentList.Add(argument);

            using var process = Process.Start(startInfo);
            var launched = process is not null;
            LoggingService.LogInformation(
                launched
                    ? $"LaunchUpdateScript: success | script='{preparation.ScriptPath}'"
                    : $"LaunchUpdateScript: failed to obtain process handle | script='{preparation.ScriptPath}'");
            return launched;
        }
        catch (Exception ex)
        {
            LoggingService.LogError(
                $"LaunchUpdateScript: failure | script='{preparation.ScriptPath}' | workingDirectory='{preparation.WorkingDirectory}'",
                ex);
            return false;
        }
    }
}
