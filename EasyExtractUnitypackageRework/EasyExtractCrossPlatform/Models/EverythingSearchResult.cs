namespace EasyExtractCrossPlatform.Models;

public sealed class EverythingSearchResult
{
    public EverythingSearchResult(
        string name,
        string fullPath,
        bool isFolder,
        bool isFile,
        long? sizeBytes,
        DateTimeOffset? lastModifiedUtc)
    {
        Name = name;
        FullPath = fullPath;
        IsFolder = isFolder;
        IsFile = isFile;
        SizeBytes = sizeBytes;
        LastModifiedUtc = lastModifiedUtc;
    }

    public string Name { get; }

    public string FullPath { get; }

    public bool IsFolder { get; }

    public bool IsFile { get; }

    public long? SizeBytes { get; }

    public DateTimeOffset? LastModifiedUtc { get; }

    public string DirectoryPath => IsFolder
        ? FullPath
        : Path.GetDirectoryName(FullPath) ?? FullPath;

    public string KindLabel => IsFolder ? "Folder" : "File";

    public string SizeLabel => SizeBytes.HasValue
        ? FormatSize(SizeBytes.Value)
        : IsFolder
            ? "--"
            : "0 B";

    public string LastModifiedLabel => LastModifiedUtc.HasValue
        ? LastModifiedUtc.Value.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)
        : "Unknown";

    private static string FormatSize(long bytes)
    {
        if (bytes < 0)
            return "Unknown";

        if (bytes < 1024)
            return $"{bytes} B";

        double readable = bytes;
        string[] suffixes = { "B", "KB", "MB", "GB", "TB", "PB" };
        var suffixIndex = 0;

        while (readable >= 1024 && suffixIndex < suffixes.Length - 1)
        {
            readable /= 1024;
            suffixIndex++;
        }

        return $"{readable:0.##} {suffixes[suffixIndex]}";
    }
}