using System.Formats.Tar;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace EasyExtractCrossPlatform.Services;

public sealed partial class MaliciousCodeDetectionService
{
    private static string NormalizeFullPath(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch
        {
            return path;
        }
    }

    private static string NormalizeAssetPath(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var sanitized = input
            .Replace("\0", string.Empty, StringComparison.Ordinal)
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", string.Empty, StringComparison.Ordinal)
            .Replace('\\', '/')
            .Trim();

        return sanitized;
    }

    private static byte[]? ReadAssetData(
        TarEntry entry,
        CancellationToken cancellationToken,
        ScanStatistics stats)
    {
        ArgumentNullException.ThrowIfNull(stats);

        if (entry.DataStream is null)
        {
            stats.AssetsSkippedMissingStream++;
            return null;
        }

        var declaredLength = entry.Length;
        if (declaredLength > MaxScannableBytes)
        {
            stats.AssetsSkippedOversize++;
            return null;
        }

        using var memoryStream = declaredLength is > 0 and <= int.MaxValue
            ? new MemoryStream((int)declaredLength)
            : new MemoryStream();

        var buffer = new byte[81920];
        int read;
        while ((read = entry.DataStream.Read(buffer, 0, buffer.Length)) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (memoryStream.Length + read > MaxScannableBytes)
            {
                stats.AssetsSkippedOversize++;
                return null;
            }

            memoryStream.Write(buffer, 0, read);
        }

        return memoryStream.ToArray();
    }

    private static string ReadPath(TarEntry entry)
    {
        if (entry.DataStream is null)
            return string.Empty;

        using var buffer = new MemoryStream();
        entry.DataStream.CopyTo(buffer);
        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static void ProcessAssetData(
        string relativePath,
        byte[] data,
        MaliciousThreatCollector collector,
        ScanStatistics stats)
    {
        ArgumentNullException.ThrowIfNull(stats);

        if (string.IsNullOrWhiteSpace(relativePath))
        {
            stats.AssetsSkippedMissingPath++;
            return;
        }

        if (!ShouldScanFile(relativePath))
        {
            stats.AssetsSkippedByExtension++;
            return;
        }

        if (!LooksLikeText(data))
        {
            stats.AssetsSkippedBinary++;
            return;
        }

        var content = Encoding.UTF8.GetString(data);
        if (string.IsNullOrWhiteSpace(content))
            return;

        stats.AssetsAnalyzed++;
        stats.TotalBytesAnalyzed += data.LongLength;
        AnalyzeTextContent(relativePath, content, collector);
    }

    private static bool ShouldScanFile(string path)
    {
        var extension = Path.GetExtension(path);
        return !string.IsNullOrWhiteSpace(extension) && ScannableExtensions.Contains(extension);
    }

    private static bool LooksLikeText(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
            return false;

        var sampleLength = Math.Min(data.Length, 4096);
        var controlCount = 0;

        for (var i = 0; i < sampleLength; i++)
        {
            var value = data[i];
            if (value == 0)
                return false;

            if (value < 9 || (value > 13 && value < 32))
                controlCount++;
        }

        return controlCount <= sampleLength * 0.2;
    }

    private static void AnalyzeTextContent(
        string relativePath,
        string content,
        MaliciousThreatCollector collector)
    {
        var discordMatches = ExtractMatches(DiscordWebhookRegex, content);
        if (discordMatches.Count > 0)
            collector.AddMatches(
                MaliciousThreatType.DiscordWebhook,
                MaliciousThreatSeverity.High,
                "Discord webhook URL detected - potential data exfiltration",
                relativePath,
                discordMatches);

        var linkMatches = ExtractMatches(LinkDetectionRegex, content);
        if (linkMatches.Count > 0)
        {
            var suspiciousLinks = linkMatches
                .Where(link => !IsAllowedLink(link))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (suspiciousLinks.Count > 0)
                collector.AddMatches(
                    MaliciousThreatType.UnsafeLinks,
                    MaliciousThreatSeverity.High,
                    "UNSAFE links detected - only links from the embedded allowed domains list are considered safe",
                    relativePath,
                    suspiciousLinks);
        }

        var suspiciousMatches = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pattern in SuspiciousPatterns)
        {
            foreach (Match match in pattern.Matches(content))
            {
                if (!match.Success || string.IsNullOrWhiteSpace(match.Value))
                    continue;

                suspiciousMatches.Add(SanitizeMatch(match.Value));
                if (suspiciousMatches.Count >= MaxMatchesPerPatternPerFile)
                    break;
            }

            if (suspiciousMatches.Count >= MaxMatchesPerPatternPerFile)
                break;
        }

        if (suspiciousMatches.Count > 0)
            collector.AddMatches(
                MaliciousThreatType.SuspiciousCodePatterns,
                MaliciousThreatSeverity.Medium,
                "Potentially dangerous API calls or patterns detected",
                relativePath,
                suspiciousMatches);
    }

    private static List<string> ExtractMatches(Regex regex, string content)
    {
        var matches = new List<string>();
        foreach (Match match in regex.Matches(content))
        {
            if (!match.Success || string.IsNullOrWhiteSpace(match.Value))
                continue;

            matches.Add(SanitizeMatch(match.Value));
            if (matches.Count >= MaxMatchesPerPatternPerFile)
                break;
        }

        return matches;
    }

    private static string SanitizeMatch(string value)
    {
        var cleaned = value
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();

        if (cleaned.Length > 200)
            cleaned = cleaned[..200] + "...";

        return cleaned;
    }

    private static bool IsAllowedLink(string link)
    {
        if (string.IsNullOrWhiteSpace(link))
            return false;

        if (!Uri.TryCreate(link, UriKind.Absolute, out var uri))
            return false;

        var host = uri.Host;
        if (string.IsNullOrWhiteSpace(host))
            return false;

        foreach (var allowed in AllowedDomains)
        {
            if (string.IsNullOrWhiteSpace(allowed))
                continue;

            var allowedHost = ExtractAllowedDomain(allowed);
            if (string.IsNullOrWhiteSpace(allowedHost))
                continue;

            if (DomainMatches(host, allowedHost))
                return true;
        }

        return false;
    }

    private static string ExtractAllowedDomain(string entry)
    {
        if (entry.Contains("://", StringComparison.Ordinal))
        {
            if (Uri.TryCreate(entry, UriKind.Absolute, out var uri))
                return uri.Host;
            return entry;
        }

        var trimmed = entry.Trim();
        var slashIndex = trimmed.IndexOf('/', StringComparison.Ordinal);
        if (slashIndex >= 0)
            trimmed = trimmed[..slashIndex];

        return trimmed.TrimStart('.');
    }

    private static bool DomainMatches(string host, string allowed)
    {
        if (string.Equals(host, allowed, StringComparison.OrdinalIgnoreCase))
            return true;

        return host.EndsWith("." + allowed, StringComparison.OrdinalIgnoreCase);
    }

    private static (string AssetKey, string ComponentName) SplitEntryName(string entryName)
    {
        var normalized = entryName.Replace('\\', '/');
        var firstSlash = normalized.IndexOf('/');
        if (firstSlash < 0)
            return (string.Empty, string.Empty);

        var key = normalized[..firstSlash].Trim();
        var remainder = normalized[(firstSlash + 1)..].Trim();
        return (key, remainder.ToLowerInvariant());
    }

}
