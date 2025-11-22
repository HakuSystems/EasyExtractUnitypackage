using System.Reflection;
using System.Runtime.InteropServices;

namespace EasyExtractCrossPlatform.Utilities;

public enum RuntimePlatform
{
    Windows,
    Linux,
    MacOS
}

public static class OperatingSystemInfo
{
    public static RuntimePlatform GetCurrentPlatform()
    {
        if (OperatingSystem.IsWindows())
            return RuntimePlatform.Windows;

        if (OperatingSystem.IsMacOS())
            return RuntimePlatform.MacOS;

        if (OperatingSystem.IsLinux())
            return RuntimePlatform.Linux;

        throw new PlatformNotSupportedException("Current platform is not supported for updates.");
    }

    public static string GetPlatformToken(RuntimePlatform platform)
    {
        return platform switch
        {
            RuntimePlatform.Windows => "win",
            RuntimePlatform.MacOS => "macos",
            RuntimePlatform.Linux => "linux",
            _ => throw new ArgumentOutOfRangeException(nameof(platform), platform, "Unknown platform.")
        };
    }

    public static string GetArchitectureToken()
    {
        return RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.X86 => "x86",
            Architecture.Arm64 => "arm64",
            Architecture.Arm => "arm",
            Architecture.S390x => "s390x",
            Architecture.Wasm => "wasm",
            _ => RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant()
        };
    }

    public static string NormalizeVersionTag(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return string.Empty;

        var trimmed = tag.Trim();
        if (trimmed.StartsWith("release/", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed[8..].TrimStart('/');

        if (trimmed.StartsWith("version", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed[7..].Trim();

        if (trimmed.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed[1..].Trim();

        return trimmed;
    }

    public static string GetApplicationName()
    {
        var entryAssemblyName = Assembly.GetEntryAssembly()?.GetName().Name;
        if (!string.IsNullOrWhiteSpace(entryAssemblyName))
            return entryAssemblyName!;

        var friendlyName = AppDomain.CurrentDomain.FriendlyName;
        if (string.IsNullOrWhiteSpace(friendlyName))
            return "EasyExtractCrossPlatform";

        var dotIndex = friendlyName.LastIndexOf('.');
        return dotIndex > 0 ? friendlyName[..dotIndex] : friendlyName;
    }
}