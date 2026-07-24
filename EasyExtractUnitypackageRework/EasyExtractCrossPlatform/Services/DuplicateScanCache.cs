using System.Text.Json;
using System.Text.Json.Serialization;
using EasyExtract.Core.Models;

namespace EasyExtractCrossPlatform.Services;

/// <summary>
///     Remembers the fingerprints of already scanned packages. Hashing a library of a few hundred
///     packages means decompressing gigabytes, so a rescan has to reuse everything that has not
///     changed on disk. An entry is only trusted while the file's size and write time still match.
/// </summary>
internal sealed class DuplicateScanCache
{
    private const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly Dictionary<string, CacheEntry> _entries = new(StringComparer.OrdinalIgnoreCase);

    public int Count => _entries.Count;

    public static string DefaultCachePath =>
        Path.Combine(AppSettingsService.SettingsDirectory, "Cache", "duplicate-index.json");

    public bool TryGet(string packagePath, FileInfo info, out PackageFingerprintResult result)
    {
        result = null!;

        if (!_entries.TryGetValue(packagePath, out var entry))
            return false;

        if (entry.SizeBytes != info.Length || entry.LastWriteUtcTicks != info.LastWriteTimeUtc.Ticks)
            return false;

        result = new PackageFingerprintResult(
            packagePath,
            (entry.Assets ?? [])
                .Select(asset => new PackageAssetFingerprint(asset.Path, asset.Hash, asset.Size))
                .ToList());
        return true;
    }

    public void Store(string packagePath, FileInfo info, PackageFingerprintResult result)
    {
        // A failed read says nothing about the file's content, so it is never cached.
        if (!result.Succeeded)
            return;

        _entries[packagePath] = new CacheEntry
        {
            SizeBytes = info.Length,
            LastWriteUtcTicks = info.LastWriteTimeUtc.Ticks,
            Assets = result.Assets
                .Select(asset => new CachedAsset
                {
                    Path = asset.AssetPath,
                    Hash = asset.ContentHash,
                    Size = asset.SizeBytes
                })
                .ToList()
        };
    }

    /// <summary>Drops entries for packages that were not part of this scan and no longer exist.</summary>
    public void PruneMissing()
    {
        foreach (var path in _entries.Keys.ToList())
            if (!File.Exists(path))
                _entries.Remove(path);
    }

    public static DuplicateScanCache Load(string? cachePath = null)
    {
        var path = cachePath ?? DefaultCachePath;
        var cache = new DuplicateScanCache();

        try
        {
            if (!File.Exists(path))
                return cache;

            var payload = JsonSerializer.Deserialize<CachePayload>(File.ReadAllText(path), SerializerOptions);
            if (payload is null || payload.Version != CurrentVersion || payload.Entries is null)
                return cache;

            foreach (var (key, entry) in payload.Entries)
                if (!string.IsNullOrWhiteSpace(key) && entry is { Assets: not null })
                    cache._entries[key] = entry;
        }
        catch (Exception ex)
        {
            // A damaged cache costs a rescan, never a failed scan.
            LoggingService.LogWarning($"Duplicate scan cache at '{path}' could not be read; starting fresh.", ex);
        }

        return cache;
    }

    public void Save(string? cachePath = null)
    {
        var path = cachePath ?? DefaultCachePath;

        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            var payload = new CachePayload
            {
                Version = CurrentVersion,
                Entries = _entries
            };

            File.WriteAllText(path, JsonSerializer.Serialize(payload, SerializerOptions));
        }
        catch (Exception ex)
        {
            LoggingService.LogWarning($"Duplicate scan cache could not be written to '{path}'.", ex);
        }
    }

    internal sealed class CachePayload
    {
        public int Version { get; set; }
        public Dictionary<string, CacheEntry>? Entries { get; set; }
    }

    internal sealed class CacheEntry
    {
        public long SizeBytes { get; set; }
        public long LastWriteUtcTicks { get; set; }
        public List<CachedAsset>? Assets { get; set; }
    }

    internal sealed class CachedAsset
    {
        public string Path { get; set; } = string.Empty;
        public string Hash { get; set; } = string.Empty;
        public long Size { get; set; }
    }
}
