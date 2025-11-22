namespace EasyExtractCrossPlatform.Services;

public sealed partial class UpdateService
{
    private static string ResolveExecutableRelativePath(string contentRoot, RuntimePlatform platform,
        string applicationName)
    {
        var resolved = platform switch
        {
            RuntimePlatform.Windows => ResolveWindowsExecutable(contentRoot, applicationName),
            RuntimePlatform.MacOS => ResolveMacExecutable(contentRoot, applicationName),
            RuntimePlatform.Linux => ResolveLinuxExecutable(contentRoot, applicationName),
            _ => throw new ArgumentOutOfRangeException(nameof(platform), platform, "Unsupported platform.")
        };

        LoggingService.LogInformation(
            $"ResolveExecutablePath: resolved | platform={platform} | application='{applicationName}' | relativePath='{resolved}'");

        return resolved;
    }

    private static string ResolveWindowsExecutable(string contentRoot, string applicationName)
    {
        var exeName = applicationName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? applicationName
            : $"{applicationName}.exe";

        var candidates = Directory.GetFiles(contentRoot, exeName, SearchOption.AllDirectories);
        if (candidates.Length == 0)
        {
            LoggingService.LogError(
                $"ResolveWindowsExecutable: missing executable | application='{applicationName}' | searchRoot='{contentRoot}'");
            throw new FileNotFoundException($"Could not find '{exeName}' in extracted payload.");
        }

        var selected = candidates.OrderBy(path => path.Length).First();
        return Path.GetRelativePath(contentRoot, selected);
    }

    private static string ResolveMacExecutable(string contentRoot, string applicationName)
    {
        var bundleName = applicationName.EndsWith(".app", StringComparison.OrdinalIgnoreCase)
            ? applicationName
            : $"{applicationName}.app";

        var bundleCandidates = Directory.GetDirectories(contentRoot, bundleName, SearchOption.AllDirectories);
        if (bundleCandidates.Length > 0)
            return Path.GetRelativePath(contentRoot, bundleCandidates.OrderBy(path => path.Length).First());

        var binaryCandidates = Directory.GetFiles(contentRoot, applicationName, SearchOption.AllDirectories);
        if (binaryCandidates.Length == 0)
        {
            LoggingService.LogError(
                $"ResolveMacExecutable: missing bundle/binary | application='{applicationName}' | searchRoot='{contentRoot}'");
            throw new FileNotFoundException(
                $"Could not locate '{applicationName}' bundle or binary in update payload.");
        }

        var selected = binaryCandidates.OrderBy(path => path.Length).First();
        return Path.GetRelativePath(contentRoot, selected);
    }

    private static string ResolveLinuxExecutable(string contentRoot, string applicationName)
    {
        var exactBinary = Directory.GetFiles(contentRoot, applicationName, SearchOption.AllDirectories);
        if (exactBinary.Length > 0)
            return Path.GetRelativePath(contentRoot, exactBinary.OrderBy(path => path.Length).First());

        var appImageCandidates =
            Directory.GetFiles(contentRoot, $"{applicationName}.AppImage", SearchOption.AllDirectories);
        if (appImageCandidates.Length > 0)
            return Path.GetRelativePath(contentRoot, appImageCandidates.OrderBy(path => path.Length).First());

        var shellCandidates = Directory.GetFiles(contentRoot, $"{applicationName}.sh", SearchOption.AllDirectories);
        if (shellCandidates.Length > 0)
            return Path.GetRelativePath(contentRoot, shellCandidates.OrderBy(path => path.Length).First());

        LoggingService.LogError(
            $"ResolveLinuxExecutable: missing executable | application='{applicationName}' | searchRoot='{contentRoot}'");
        throw new FileNotFoundException($"Could not locate '{applicationName}' executable in update payload.");
    }

    private static string ResolveInstallDirectory()
    {
        var assemblyLocation = AppContext.BaseDirectory;
        if (string.IsNullOrWhiteSpace(assemblyLocation))
        {
            LoggingService.LogError("ResolveInstallDirectory: base directory is unavailable.");
            throw new InvalidOperationException("Unable to determine application directory for installation.");
        }

        var resolved = Path.GetFullPath(assemblyLocation);
        LoggingService.LogInformation($"ResolveInstallDirectory: resolved | path='{resolved}'");
        return resolved;
    }
}