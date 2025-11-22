namespace EasyExtractCrossPlatform.ViewModels;

public sealed class UnityPackageAssetPreviewItem
{
    public UnityPackageAssetPreviewItem(UnityPackagePreviewAsset asset)
    {
        if (asset is null)
            throw new ArgumentNullException(nameof(asset));

        RelativePath = asset.RelativePath;
        FileName = Path.GetFileName(RelativePath);
        Directory = Path.GetDirectoryName(RelativePath) ?? string.Empty;
        AssetSizeBytes = asset.AssetSizeBytes;
        HasMetaFile = asset.HasMetaFile;
        PreviewImageData = asset.PreviewImageData;
        AssetData = asset.AssetData;
        IsAssetDataTruncated = asset.IsAssetDataTruncated;
        AssetFilePath = asset.AssetFilePath;

        Extension = Path.GetExtension(RelativePath)?.ToLowerInvariant() ?? string.Empty;
        Category = UnityAssetClassification.ResolveCategory(
            asset.RelativePath,
            asset.AssetSizeBytes,
            asset.AssetData is { Length: > 0 });
        SizeText = FormatFileSize(AssetSizeBytes);
    }

    public string RelativePath { get; }

    public string FileName { get; }

    public string Directory { get; }

    public long AssetSizeBytes { get; }

    public bool HasMetaFile { get; }

    public byte[]? PreviewImageData { get; }

    public byte[]? AssetData { get; }

    public string? AssetFilePath { get; }

    public bool IsAssetDataTruncated { get; }

    public string Extension { get; }

    public string Category { get; }

    public bool HasPreview => PreviewImageData is { Length: > 0 };

    public string SizeText { get; }

    public Geometry IconGeometry => UnityAssetIconProvider.GetAssetIcon(Category);

    [SuppressMessage("Globalization", "CA1305:Specify IFormatProvider")]
    private static string FormatFileSize(long bytes)
    {
        if (bytes <= 0)
            return "0 B";

        var units = new[] { "B", "KB", "MB", "GB", "TB", "PB" };
        var size = (double)bytes;
        var unitIndex = 0;

        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return unitIndex == 0
            ? $"{bytes} {units[unitIndex]}"
            : $"{size:0.##} {units[unitIndex]}";
    }
}