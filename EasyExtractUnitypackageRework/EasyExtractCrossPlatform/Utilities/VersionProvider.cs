using System.Reflection;

namespace EasyExtractCrossPlatform.Utilities;

public static class VersionProvider
{
    public static string GetApplicationVersion()
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();

        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informationalVersion))
            return informationalVersion;

        return assembly.GetName().Version?.ToString() ?? string.Empty;
    }
}