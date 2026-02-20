using System.Buffers;
using System.Collections.Concurrent;
using System.Formats.Tar;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using EasyExtract.Core.Models;

namespace EasyExtract.Core.Services;

public interface IMaliciousCodeDetectionService
{
    Task<MaliciousCodeScanResult> ScanPackageAsync(
        string packagePath,
        CancellationToken cancellationToken = default);
}

public sealed class MaliciousCodeDetectionService : IMaliciousCodeDetectionService
{
    private const long MaxScannableBytes = 5 * 1024 * 1024; // 5 MB per file limit
    private const int MaxMatchesPerPatternPerFile = 5;

    private static readonly Regex DiscordWebhookRegex = new(
        @"https:\/\/discord(?:app)?\.com\/api\/webhooks\/\d{18}\/[A-Za-z0-9\-_]{68}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex LinkDetectionRegex = new(
        @"https?:\/\/(?:www\.)?[-a-zA-Z0-9@:%._\+~#=]{1,256}\.[a-z]{2,63}\b(?:[-a-zA-Z0-9@:%_\+.~#?&//=]*)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex[] SuspiciousPatterns =
    {
        new(@"UnityWebRequest", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"File\.Delete", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"Directory\.Delete", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"Process\.Start", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"RegistryKey", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"WebClient", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"HttpClient", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"Socket", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"System\.Reflection", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"DllImport", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
    };

    private static readonly HashSet<string> ScannableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".js", ".boo", ".dll.txt" // dll.txt sometimes used for stealth
    };

    private static readonly string[] AllowedDomains =
    {
        "unity.com", "unity3d.com", "github.com", "google.com", "microsoft.com", "stackoverflow.com",
        "twitter.com", "x.com", "youtube.com", "discord.gg", "discord.com", "patreon.com", "ko-fi.com",
        "paypal.com", "imgur.com", "pastebin.com", "hastebin.com", "gist.github.com"
    };

    private static readonly SearchValues<string> FastPathKeywords = SearchValues.Create(
    [
        "discord", "http://", "https://", "UnityWebRequest", "File.Delete",
        "Directory.Delete", "Process.Start", "RegistryKey", "WebClient",
        "HttpClient", "Socket", "System.Reflection", "DllImport"
    ], StringComparison.OrdinalIgnoreCase);

    private readonly IEasyExtractLogger _logger;

    private readonly ConcurrentDictionary<string, Task<MaliciousCodeScanResult>> _pendingScans =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<string, CachedScanResult> _scanCache =
        new(StringComparer.OrdinalIgnoreCase);

    public MaliciousCodeDetectionService(IEasyExtractLogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<MaliciousCodeScanResult> ScanPackageAsync(
        string packagePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(packagePath))
            throw new ArgumentException("Path cannot be null or empty.", nameof(packagePath));

        if (!File.Exists(packagePath))
            throw new FileNotFoundException("Unitypackage file not found.", packagePath);

        if (_scanCache.TryGetValue(packagePath, out var cached))
        {
            // Simple cache validity check (5 minutes)
            if (DateTimeOffset.UtcNow - cached.Timestamp < TimeSpan.FromMinutes(5))
            {
                _logger.LogInformation($"Returned cached scan result for '{packagePath}'.");
                return cached.Result;
            }

            _scanCache.TryRemove(packagePath, out _);
        }

        return await _pendingScans.GetOrAdd(packagePath,
            path => ScanPackageInternalAsync(path, cancellationToken));
    }

    private async Task<MaliciousCodeScanResult> ScanPackageInternalAsync(
        string packagePath,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation($"Starting malicious code scan for '{packagePath}'.");

            var result = await ScanPackageStreamAsync(packagePath, cancellationToken)
                .ConfigureAwait(false);

            _scanCache[packagePath] = new CachedScanResult(result, DateTimeOffset.UtcNow);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Scan failed for '{packagePath}'.", ex);
            throw;
        }
        finally
        {
            _pendingScans.TryRemove(packagePath, out _);
        }
    }

    private async Task<MaliciousCodeScanResult> ScanPackageStreamAsync(string packagePath,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = new FileStream(packagePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            await using var gzipStream = new GZipStream(stream, CompressionMode.Decompress);
            using var tarReader = new TarReader(gzipStream);

            var collector = new MaliciousThreatCollector();
            var stats = new ScanStatistics();
            var assetStates = new Dictionary<string, AssetSecurityState>(StringComparer.OrdinalIgnoreCase);

            TarEntry? entry;
            while ((entry = await tarReader.GetNextEntryAsync(false, cancellationToken)) is not null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                stats.TarEntriesRead++;

                if (entry.EntryType is not (TarEntryType.RegularFile or TarEntryType.V7RegularFile))
                    continue;

                var entryName = entry.Name ?? string.Empty;
                if (string.IsNullOrWhiteSpace(entryName))
                    continue;

                var (assetKey, componentName) = SplitEntryName(entryName);
                if (string.IsNullOrWhiteSpace(assetKey))
                    continue;

                if (!assetStates.TryGetValue(assetKey, out var state))
                {
                    state = new AssetSecurityState();
                    assetStates[assetKey] = state;
                }

                if (componentName == "pathname")
                {
                    stats.PathComponentsSeen++;
                    var path = await ReadPathAsync(entry, cancellationToken);
                    state.RelativePath = NormalizeFullPath(NormalizeAssetPath(path));

                    // If we have pending data, process it now that we have the path
                    if (state.PendingAssetData is not null)
                    {
                        ProcessAssetData(state.RelativePath, state.PendingAssetData, collector, stats);
                        state.PendingAssetData = null; // Clear to save memory
                    }
                }
                else if (componentName == "asset")
                {
                    stats.AssetComponentsSeen++;
                    var data = await ReadAssetDataAsync(entry, cancellationToken, stats);

                    if (data is null)
                        continue;

                    if (!string.IsNullOrWhiteSpace(state.RelativePath))
                        ProcessAssetData(state.RelativePath, data, collector, stats);
                    else
                        // Buffer data until path is found
                        state.PendingAssetData = data;
                }
            }

            var threats = collector.BuildResults();
            var isMalicious = threats.Count > 0;

            _logger.LogInformation(
                $"Scan completed for '{packagePath}'. Threats={threats.Count}, Malicious={isMalicious}. Stats: {stats.ToPerformanceDetails()}");

            return new MaliciousCodeScanResult(
                packagePath,
                isMalicious,
                threats,
                DateTimeOffset.UtcNow);
        }
        catch (InvalidDataException ex)
        {
            // Some Unity packages use compression methods not supported by .NET's GZipStream/TarReader
            // (e.g., LZMA, LZ4 from Asset Bundles). Return a "scan skipped" result instead of crashing.
            _logger.LogWarning($"Scan skipped for '{packagePath}': unsupported compression format. {ex.Message}");

            return new MaliciousCodeScanResult(
                packagePath,
                false,
                new List<MaliciousThreat>(),
                DateTimeOffset.UtcNow,
                true,
                "Package uses unsupported compression format. Manual review recommended.");
        }
    }

    // --- AssetProcessing Partials ---

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

    private static async Task<byte[]?> ReadAssetDataAsync(
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

        // ⚡ Bolt: Utilize ArrayPool to prevent massive GC allocations for thousands of small assets.
        var buffer = ArrayPool<byte>.Shared.Rent(81920);
        try
        {
            int read;
            while ((read = await entry.DataStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                if (memoryStream.Length + read > MaxScannableBytes)
                {
                    stats.AssetsSkippedOversize++;
                    return null;
                }

                memoryStream.Write(buffer, 0, read);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        return memoryStream.ToArray();
    }

    private static async Task<string> ReadPathAsync(TarEntry entry, CancellationToken cancellationToken)
    {
        if (entry.DataStream is null)
            return string.Empty;

        // ⚡ Bolt: Use StreamReader to avoid allocating entire byte[] in MemoryStream just to decode UTF8.
        using var reader = new StreamReader(entry.DataStream, Encoding.UTF8, true, 1024, true);
        return await reader.ReadToEndAsync(cancellationToken);
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
        if (!content.AsSpan().ContainsAny(FastPathKeywords))
            return;

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

    // --- Collectors Partials ---

    private sealed class ScanStatistics
    {
        public int TarEntriesRead { get; set; }
        public int PathComponentsSeen { get; set; }
        public int AssetComponentsSeen { get; set; }
        public int AssetsAnalyzed { get; set; }
        public int AssetsSkippedByExtension { get; set; }
        public int AssetsSkippedBinary { get; set; }
        public int AssetsSkippedMissingPath { get; set; }
        public int AssetsSkippedOversize { get; set; }
        public int AssetsSkippedMissingStream { get; set; }
        public long TotalBytesAnalyzed { get; set; }
        public int ThreatCount { get; set; }

        public string ToPerformanceDetails()
        {
            return
                $"entries={TarEntriesRead}|assets={AssetComponentsSeen}|analyzed={AssetsAnalyzed}|skipExt={AssetsSkippedByExtension}|skipBinary={AssetsSkippedBinary}|skipMissingPath={AssetsSkippedMissingPath}|skipSize={AssetsSkippedOversize}|skipStream={AssetsSkippedMissingStream}|threats={ThreatCount}";
        }
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