using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using EasyExtractCrossPlatform.Models;

namespace EasyExtractCrossPlatform.Services;

public interface IMaliciousCodeDetectionService
{
    Task<MaliciousCodeScanResult> ScanUnityPackageAsync(
        string packagePath,
        CancellationToken cancellationToken = default);
}

public sealed class MaliciousCodeDetectionService : IMaliciousCodeDetectionService
{
    private const long MaxScannableBytes = 4 * 1024 * 1024;
    private const int MaxMatchesPerPatternPerFile = 25;

    private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(5);

    private static readonly Regex DiscordWebhookRegex = new(
        @"https:\/\/discord(?:app)?\.com\/api\/webhooks\/\d{18}\/[A-Za-z0-9\-_]{68}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex LinkDetectionRegex = new(
        @"https?:\/\/(?:www\.)?[-a-zA-Z0-9@:%._\+~#=]{1,256}\.[a-z]{2,63}\b(?:[-a-zA-Z0-9@:%_\+.~#?&//=]*)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex[] SuspiciousPatterns =
    {
        new(@"UnityWebRequest", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"HttpClient", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"WebClient", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"HttpWebRequest", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"RestClient", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"WWW\s*\(", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"File\.WriteAllText", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"File\.WriteAllBytes", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"File\.Delete", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"Directory\.Delete", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"Process\.Start", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"Registry\.", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"RegistryKey", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"Application\.persistentDataPath",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"Application\.dataPath", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"Environment\.GetFolderPath",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"System\.Reflection", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"Assembly\.Load", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"Activator\.CreateInstance",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"Convert\.FromBase64String",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"Encoding\.UTF8\.GetString",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"System\.Text\.Encoding", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
    };

    private static readonly HashSet<string> ScannableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".js", ".boo", ".shader", ".cginc", ".hlsl",
        ".txt", ".json", ".xml", ".yaml", ".yml",
        ".asset", ".unity", ".prefab", ".mat",
        ".asmdef", ".asmref"
    };

    private static readonly string[] AllowedDomains =
    {
        "unity3d.com",
        "unity.com",
        "assetstore.unity3d.com",
        "connect.unity.com",
        "developer.unity3d.com",
        "docs.unity3d.com",
        "forum.unity.com",
        "id.unity.com",
        "learn.unity.com",
        "support.unity3d.com",
        "microsoft.com",
        "docs.microsoft.com",
        "dotnet.microsoft.com",
        "nuget.org",
        "visualstudio.com",
        "azure.microsoft.com",
        "github.com",
        "gitlab.com",
        "bitbucket.org",
        "sourceforge.net",
        "codeplex.com",
        "npmjs.com",
        "npmjs.org",
        "yarnpkg.com",
        "packagist.org",
        "pypi.org",
        "stackoverflow.com",
        "stackexchange.com",
        "developer.mozilla.org",
        "w3schools.com",
        "tutorialspoint.com",
        "gamedev.net",
        "gamasutra.com",
        "indiedb.com",
        "moddb.com",
        "itch.io",
        "freesound.org",
        "opengameart.org",
        "kenney.nl",
        "mixamo.com",
        "sketchfab.com",
        "amazonaws.com",
        "googlecloud.com",
        "firebase.google.com",
        "heroku.com",
        "netlify.com",
        "vercel.com",
        "cloudflare.com",
        "jsdelivr.net",
        "unpkg.com",
        "cdnjs.cloudflare.com",
        "maxcdn.bootstrapcdn.com",
        "google.com",
        "googleapis.com",
        "googletagmanager.com",
        "google-analytics.com",
        "fonts.googleapis.com",
        "fonts.gstatic.com",
        "discord.gg",
        "reddit.com",
        "twitter.com",
        "youtube.com",
        "twitch.tv",
        "edu",
        "ac.uk",
        "mit.edu",
        "stanford.edu",
        "coursera.org",
        "udemy.com",
        "edx.org",
        "apache.org",
        "mozilla.org",
        "gnu.org",
        "fsf.org",
        "opensource.org",
        "w3.org",
        "whatwg.org",
        "ietf.org",
        "iso.org",
        "jetbrains.com",
        "atlassian.com",
        "slack.com",
        "trello.com",
        "notion.so",
        "letsencrypt.org",
        "digicert.com",
        "symantec.com",
        "verisign.com"
    };

    private readonly ConcurrentDictionary<string, Task<MaliciousCodeScanResult>> _inFlightScans =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<string, CachedScanResult> _scanCache = new(StringComparer.OrdinalIgnoreCase);

    public Task<MaliciousCodeScanResult> ScanUnityPackageAsync(
        string packagePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);

        var normalizedPath = NormalizeFullPath(packagePath);
        if (!File.Exists(normalizedPath))
            throw new FileNotFoundException("Unitypackage file was not found.", normalizedPath);

        if (_scanCache.TryGetValue(normalizedPath, out var cached) &&
            DateTimeOffset.UtcNow - cached.Timestamp < CacheExpiration)
            return Task.FromResult(cached.Result);

        var scanTask = _inFlightScans.GetOrAdd(
            normalizedPath,
            _ => RunScanAsync(normalizedPath, cancellationToken));

        return scanTask;
    }

    private async Task<MaliciousCodeScanResult> RunScanAsync(
        string packagePath,
        CancellationToken cancellationToken)
    {
        try
        {
            LoggingService.LogInformation($"Security scan started for '{packagePath}'.");
            var result = await Task.Run(() => ScanUnityPackageCore(packagePath, cancellationToken), cancellationToken)
                .ConfigureAwait(false);
            _scanCache[packagePath] = new CachedScanResult(result, DateTimeOffset.UtcNow);
            LoggingService.LogInformation(
                $"Security scan completed for '{packagePath}'. Threats={result.Threats.Count}.");
            return result;
        }
        finally
        {
            _inFlightScans.TryRemove(packagePath, out _);
        }
    }

    private static MaliciousCodeScanResult ScanUnityPackageCore(string packagePath, CancellationToken cancellationToken)
    {
        var collector = new MaliciousThreatCollector();
        var assetStates = new Dictionary<string, AssetSecurityState>(StringComparer.OrdinalIgnoreCase);

        using var packageStream = File.OpenRead(packagePath);
        using var gzipStream = new GZipStream(packageStream, CompressionMode.Decompress);
        using var tarReader = new TarReader(gzipStream, false);

        TarEntry? entry;
        while ((entry = tarReader.GetNextEntry()) is not null)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (entry.EntryType == TarEntryType.Directory)
                continue;

            var entryName = entry.Name ?? string.Empty;
            if (string.IsNullOrWhiteSpace(entryName))
                continue;

            var (assetKey, componentName) = SplitEntryName(entryName);
            if (string.IsNullOrWhiteSpace(assetKey) || string.IsNullOrWhiteSpace(componentName))
                continue;

            if (!assetStates.TryGetValue(assetKey, out var state))
            {
                state = new AssetSecurityState();
                assetStates[assetKey] = state;
            }

            switch (componentName)
            {
                case "pathname" when entry.DataStream is not null:
                {
                    var relativePath = ReadPath(entry);
                    state.RelativePath = NormalizeAssetPath(relativePath);
                    if (state.PendingAssetData is { Length: > 0 } pendingData &&
                        !string.IsNullOrWhiteSpace(state.RelativePath))
                    {
                        ProcessAssetData(state.RelativePath, pendingData, collector);
                        state.PendingAssetData = null;
                        assetStates.Remove(assetKey);
                    }

                    break;
                }
                case "asset" when entry.DataStream is not null:
                {
                    var data = ReadAssetData(entry, cancellationToken);
                    if (data is null || data.Length == 0)
                        break;

                    if (!string.IsNullOrWhiteSpace(state.RelativePath))
                    {
                        ProcessAssetData(state.RelativePath, data, collector);
                        assetStates.Remove(assetKey);
                    }
                    else
                    {
                        state.PendingAssetData = data;
                    }

                    break;
                }
            }
        }

        var threats = collector.BuildResults();
        return new MaliciousCodeScanResult(
            packagePath,
            threats.Count > 0,
            threats,
            DateTimeOffset.UtcNow);
    }

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

    private static byte[]? ReadAssetData(TarEntry entry, CancellationToken cancellationToken)
    {
        if (entry.DataStream is null)
            return null;

        var declaredLength = entry.Length;
        if (declaredLength > MaxScannableBytes)
            return null;

        using var memoryStream = declaredLength is > 0 and <= int.MaxValue
            ? new MemoryStream((int)declaredLength)
            : new MemoryStream();

        var buffer = new byte[81920];
        int read;
        while ((read = entry.DataStream.Read(buffer, 0, buffer.Length)) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (memoryStream.Length + read > MaxScannableBytes)
                return null;

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
        MaliciousThreatCollector collector)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return;

        if (!ShouldScanFile(relativePath))
            return;

        if (!LooksLikeText(data))
            return;

        var content = Encoding.UTF8.GetString(data);
        if (string.IsNullOrWhiteSpace(content))
            return;

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

    private sealed record CachedScanResult(MaliciousCodeScanResult Result, DateTimeOffset Timestamp);

    private sealed class AssetSecurityState
    {
        public string? RelativePath { get; set; }
        public byte[]? PendingAssetData { get; set; }
    }

    private sealed class MaliciousThreatCollector
    {
        private readonly Dictionary<MaliciousThreatType, ThreatAccumulator> _accumulators = new();

        public void AddMatches(
            MaliciousThreatType type,
            MaliciousThreatSeverity severity,
            string description,
            string filePath,
            IEnumerable<string> matches)
        {
            if (!matches.Any())
                return;

            if (!_accumulators.TryGetValue(type, out var accumulator))
            {
                accumulator = new ThreatAccumulator(type, severity, description);
                _accumulators[type] = accumulator;
            }

            accumulator.AddMatches(filePath, matches);
        }

        public List<MaliciousThreat> BuildResults()
        {
            var results = new List<MaliciousThreat>();
            foreach (var accumulator in _accumulators.Values)
            {
                if (!accumulator.HasMatches)
                    continue;

                results.Add(new MaliciousThreat(
                    accumulator.Type,
                    accumulator.Severity,
                    accumulator.Description,
                    accumulator.ToMatches()));
            }

            return results;
        }
    }

    private sealed class ThreatAccumulator
    {
        private const int MaxMatchesPerThreat = 50;

        private readonly Dictionary<string, HashSet<string>> _matchesByFile =
            new(StringComparer.OrdinalIgnoreCase);

        private int _totalMatches;

        public ThreatAccumulator(
            MaliciousThreatType type,
            MaliciousThreatSeverity severity,
            string description)
        {
            Type = type;
            Severity = severity;
            Description = description;
        }

        public MaliciousThreatType Type { get; }
        public MaliciousThreatSeverity Severity { get; }
        public string Description { get; }

        public bool HasMatches => _totalMatches > 0;

        public void AddMatches(string filePath, IEnumerable<string> matches)
        {
            if (_totalMatches >= MaxMatchesPerThreat)
                return;

            var path = NormalizeAssetPath(filePath);
            if (string.IsNullOrWhiteSpace(path))
                path = "Unknown asset";

            if (!_matchesByFile.TryGetValue(path, out var set))
            {
                set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _matchesByFile[path] = set;
            }

            foreach (var match in matches)
            {
                if (_totalMatches >= MaxMatchesPerThreat)
                    break;

                if (string.IsNullOrWhiteSpace(match))
                    continue;

                if (set.Add(match))
                    _totalMatches++;
            }
        }

        public IReadOnlyList<MaliciousThreatMatch> ToMatches()
        {
            var matches = new List<MaliciousThreatMatch>(_totalMatches);
            foreach (var kvp in _matchesByFile.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
            {
                var snippets = kvp.Value.ToList();
                snippets.Sort(StringComparer.OrdinalIgnoreCase);
                foreach (var snippet in snippets)
                    matches.Add(new MaliciousThreatMatch(kvp.Key, snippet));
            }

            return matches;
        }
    }
}