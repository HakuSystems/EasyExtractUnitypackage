namespace EasyExtractCrossPlatform.Models;

public sealed record class UnityPackageExtractionLimits
{
    public const long DefaultMaxAssetBytes = 256L * 1024 * 1024; // 256 MB
    public const long DefaultMaxPackageBytes = 4L * 1024 * 1024 * 1024; // 4 GB
    public const int DefaultMaxAssets = 20000;

    public const long MaxAllowedAssetBytes = 1024L * 1024 * 1024; // 1 GB per asset
    public const long MaxAllowedPackageBytes = 64L * 1024 * 1024 * 1024; // 64 GB per package
    public const int MaxAllowedAssets = 200000;

    public static UnityPackageExtractionLimits Default => new();

    public long MaxAssetBytes { get; set; } = DefaultMaxAssetBytes;
    public long MaxPackageBytes { get; set; } = DefaultMaxPackageBytes;
    public int MaxAssets { get; set; } = DefaultMaxAssets;

    public static UnityPackageExtractionLimits Normalize(UnityPackageExtractionLimits? candidate)
    {
        var limits = candidate ?? new UnityPackageExtractionLimits();

        limits.MaxAssetBytes = NormalizeBytes(limits.MaxAssetBytes, DefaultMaxAssetBytes, MaxAllowedAssetBytes);
        limits.MaxPackageBytes = NormalizeBytes(limits.MaxPackageBytes, DefaultMaxPackageBytes, MaxAllowedPackageBytes);
        if (limits.MaxPackageBytes < limits.MaxAssetBytes)
            limits.MaxPackageBytes = limits.MaxAssetBytes;

        limits.MaxAssets = NormalizeCount(limits.MaxAssets, DefaultMaxAssets, MaxAllowedAssets);
        return limits;
    }

    private static long NormalizeBytes(long value, long fallback, long max)
    {
        if (value <= 0)
            return fallback;

        return value > max ? max : value;
    }

    private static int NormalizeCount(int value, int fallback, int max)
    {
        if (value <= 0)
            return fallback;

        return value > max ? max : value;
    }
}