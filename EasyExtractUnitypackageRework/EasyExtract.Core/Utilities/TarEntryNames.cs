namespace EasyExtract.Core.Utilities;

public static class TarEntryNames
{
    /// <summary>
    ///     Unity writes its tar entries as "guid/component", but packages repacked with bsdtar
    ///     (anything that went through macOS) carry a "./" root prefix. Left in place, the guid is no
    ///     longer the first segment and every entry of the package collapses into one asset named ".".
    /// </summary>
    public static string StripArchiveRootPrefix(string entryName)
    {
        var normalized = entryName.TrimStart('/');
        while (normalized.StartsWith("./", StringComparison.Ordinal))
            normalized = normalized[2..].TrimStart('/');

        return normalized;
    }
}
