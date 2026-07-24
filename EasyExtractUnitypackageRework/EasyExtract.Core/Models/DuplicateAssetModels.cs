namespace EasyExtract.Core.Models;

/// <summary>
///     One asset inside one package, identified by the hash of its content.
/// </summary>
public sealed record PackageAssetFingerprint(
    string AssetPath,
    string ContentHash,
    long SizeBytes);

/// <summary>
///     The fingerprints of a single package. A package that could not be read is reported with an
///     <see cref="Error" /> instead of being dropped, so a broken file in the library stays visible.
/// </summary>
public sealed record PackageFingerprintResult(
    string PackagePath,
    IReadOnlyList<PackageAssetFingerprint> Assets,
    string? Error = null)
{
    public bool Succeeded => Error is null;
}

/// <summary>
///     Where a duplicated asset was found.
/// </summary>
public sealed record DuplicateAssetOccurrence(string PackagePath, string AssetPath)
{
    public string PackageName => Path.GetFileName(PackagePath);
}

/// <summary>
///     A group of assets that share the same content.
/// </summary>
public sealed record DuplicateAssetGroup(
    string ContentHash,
    long SizeBytes,
    IReadOnlyList<DuplicateAssetOccurrence> Occurrences)
{
    /// <summary>Keeping one copy is not waste, every further copy is.</summary>
    public long WastedBytes => SizeBytes * (Occurrences.Count - 1);

    public int DistinctPackageCount =>
        Occurrences.Select(occurrence => occurrence.PackagePath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

    /// <summary>True when the copies are spread over more than one package.</summary>
    public bool SpansMultiplePackages => DistinctPackageCount > 1;

    /// <summary>The shortest asset path in the group, used as the group's display name.</summary>
    public string DisplayName =>
        Occurrences
            .Select(occurrence => occurrence.AssetPath)
            .OrderBy(path => path.Length)
            .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault() ?? ContentHash;
}

/// <summary>
///     The result of scanning a set of packages for duplicated assets.
/// </summary>
public sealed record DuplicateScanReport(
    IReadOnlyList<DuplicateAssetGroup> Groups,
    int PackagesScanned,
    int PackagesFailed,
    IReadOnlyList<string> FailedPackages)
{
    public static DuplicateScanReport Empty { get; } =
        new(Array.Empty<DuplicateAssetGroup>(), 0, 0, Array.Empty<string>());

    public long TotalWastedBytes => Groups.Sum(group => group.WastedBytes);

    public int TotalDuplicateCopies => Groups.Sum(group => group.Occurrences.Count - 1);

    public bool HasFindings => Groups.Count > 0;
}

/// <summary>
///     Progress while scanning a library.
/// </summary>
public sealed record DuplicateScanProgress(
    int PackagesCompleted,
    int PackagesTotal,
    string CurrentPackageName)
{
    public double Fraction => PackagesTotal <= 0
        ? 0d
        : Math.Clamp((double)PackagesCompleted / PackagesTotal, 0d, 1d);
}
