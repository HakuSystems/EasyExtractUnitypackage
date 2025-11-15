using System;
using System.IO;
using System.Text.Json.Serialization;

namespace EasyExtractCrossPlatform.Models;

public class HistoryEntry
{
    private static readonly StringComparer PathComparer = StringComparer.OrdinalIgnoreCase;

    public Guid Id { get; set; } = Guid.NewGuid();
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DateTimeOffset AddedUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastSeenUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ExtractedUtc { get; set; }
    public bool WasExtracted { get; set; }
    public int AssetsExtracted { get; set; }
    public int ExtractedFilesCount { get; set; }
    public long ExtractedBytes { get; set; }
    public double ExtractionDurationMs { get; set; }
    public string OutputDirectory { get; set; } = string.Empty;

    [JsonIgnore] public bool HasExtractionSnapshot => WasExtracted && ExtractedUtc is not null;

    [JsonIgnore] public double FileSizeMegabytes => FileSizeBytes <= 0 ? 0 : FileSizeBytes / 1024d / 1024d;

    [JsonIgnore] public double ExtractedMegabytes => ExtractedBytes <= 0 ? 0 : ExtractedBytes / 1024d / 1024d;

    public static HistoryEntry Create(FileInfo info, string normalizedPath, DateTimeOffset timestampUtc)
    {
        return new HistoryEntry
        {
            Id = Guid.NewGuid(),
            FileName = info.Name,
            FilePath = normalizedPath,
            FileSizeBytes = info.Exists ? info.Length : 0,
            AddedUtc = timestampUtc,
            LastSeenUtc = timestampUtc
        };
    }

    public static HistoryEntry FromLegacyPath(string? legacyPath, DateTimeOffset migrationTimestamp)
    {
        if (string.IsNullOrWhiteSpace(legacyPath))
        {
            var placeholder = Path.Combine(Path.GetTempPath(), "EasyExtractHistory_Unknown.unitypackage");
            return Create(new FileInfo(placeholder), placeholder, migrationTimestamp);
        }

        FileInfo info;
        try
        {
            info = new FileInfo(legacyPath);
        }
        catch
        {
            var placeholder = Path.Combine(Path.GetTempPath(), "EasyExtractHistory_Invalid.unitypackage");
            info = new FileInfo(placeholder);
        }

        var normalized = NormalizePathSafe(legacyPath) ?? legacyPath;
        return Create(info, normalized, migrationTimestamp);
    }

    public bool MatchesPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        return PathComparer.Equals(FilePath, path);
    }

    public void Touch(FileInfo info, DateTimeOffset timestampUtc)
    {
        FileName = info.Name;
        FileSizeBytes = info.Exists ? info.Length : FileSizeBytes;
        LastSeenUtc = timestampUtc;
    }

    public void CaptureExtractionSnapshot(
        int assetsExtracted,
        int filesWritten,
        long extractedBytes,
        TimeSpan duration,
        string outputDirectory,
        DateTimeOffset timestampUtc)
    {
        AssetsExtracted = Math.Max(0, assetsExtracted);
        ExtractedFilesCount = Math.Max(0, filesWritten);
        ExtractedBytes = Math.Max(0, extractedBytes);
        ExtractionDurationMs = Math.Max(0, duration.TotalMilliseconds);
        OutputDirectory = outputDirectory;
        WasExtracted = true;
        ExtractedUtc = timestampUtc;
    }

    public static string? NormalizePathSafe(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        try
        {
            return Path.GetFullPath(path);
        }
        catch
        {
            return path;
        }
    }
}