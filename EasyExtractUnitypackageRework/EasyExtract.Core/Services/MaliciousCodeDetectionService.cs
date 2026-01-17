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
using EasyExtract.Core.Models;

namespace EasyExtract.Core.Services;

public interface IMaliciousCodeDetectionService
{
    Task<MaliciousCodeScanResult> ScanPackageAsync(
        string packagePath,
        CancellationToken cancellationToken = default);
}

public sealed partial class MaliciousCodeDetectionService : IMaliciousCodeDetectionService
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

    private readonly ConcurrentDictionary<string, CachedScanResult> _scanCache =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<string, Task<MaliciousCodeScanResult>> _pendingScans =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly IEasyExtractLogger _logger;

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

            var result = await Task.Run(() => ScanSync(packagePath, cancellationToken), cancellationToken)
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

    private MaliciousCodeScanResult ScanSync(string packagePath, CancellationToken cancellationToken)
    {
        using var stream = File.OpenRead(packagePath);
        using var gzipStream = new GZipStream(stream, CompressionMode.Decompress);
        using var tarReader = new TarReader(gzipStream);

        var collector = new MaliciousThreatCollector();
        var stats = new ScanStatistics();
        var assetStates = new Dictionary<string, AssetSecurityState>(StringComparer.OrdinalIgnoreCase);

        TarEntry? entry;
        while ((entry = tarReader.GetNextEntry()) is not null)
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
                var path = ReadPath(entry);
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
                var data = ReadAssetData(entry, cancellationToken, stats);

                if (data is null)
                    continue;

                if (!string.IsNullOrWhiteSpace(state.RelativePath))
                {
                    ProcessAssetData(state.RelativePath, data, collector, stats);
                }
                else
                {
                    // Buffer data until path is found
                    state.PendingAssetData = data;
                }
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
}
