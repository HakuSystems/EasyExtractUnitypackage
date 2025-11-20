using System.Diagnostics;
using System.Runtime.Versioning;

namespace EasyExtractCrossPlatform.Services;

public static partial class ContextMenuIntegrationService
{
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
            LoggingService.LogError("Failed to resolve Windows icon candidate.", ex);
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
                LoggingService.LogInformation($"Skipping execution of '{fileName}' because it was not found on PATH.");
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
            LoggingService.LogError($"Failed to execute '{fileName} {arguments}'.", ex);
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
            LoggingService.LogError($"Failed to probe PATH for '{command}'.", ex);
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
            LoggingService.LogError($"Failed to delete file '{path}'.", ex);
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
            LoggingService.LogError($"Failed to delete directory '{path}'.", ex);
        }
    }
}
