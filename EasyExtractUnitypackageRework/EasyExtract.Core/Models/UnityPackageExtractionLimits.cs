namespace EasyExtract.Core.Models;

public sealed record class UnityPackageExtractionLimits
{
    public const long DefaultMaxAssetBytes = 512L * 1024 * 1024; // 512 MB
    public const long DefaultMaxPackageBytes = 32L * 1024 * 1024 * 1024; // 32 GB
    public const int DefaultMaxAssets = 25000;

    public const long MaxAllowedAssetBytes = 4L * 1024 * 1024 * 1024; // 4 GB
    public const long MaxAllowedPackageBytes = 64L * 1024 * 1024 * 1024; // 64 GB
    public const int MaxAllowedAssets = 200000;

    // Old default that shipped up to V2.9.3; stored settings carrying it are lifted
    // to the current default because real-world packages regularly exceeded 16 GB.
    private const long LegacyDefaultMaxPackageBytes = 16L * 1024 * 1024 * 1024;

    public static UnityPackageExtractionLimits Default => new();

    public long MaxAssetBytes { get; set; } = DefaultMaxAssetBytes;
    public long MaxPackageBytes { get; set; } = DefaultMaxPackageBytes;
    public int MaxAssets { get; set; } = DefaultMaxAssets;

    public static UnityPackageExtractionLimits Normalize(UnityPackageExtractionLimits? candidate)
    {
        var limits = candidate ?? new UnityPackageExtractionLimits();

        limits.MaxAssetBytes = NormalizeBytes(limits.MaxAssetBytes, DefaultMaxAssetBytes, MaxAllowedAssetBytes);
        if (limits.MaxPackageBytes == LegacyDefaultMaxPackageBytes)
            limits.MaxPackageBytes = DefaultMaxPackageBytes;
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