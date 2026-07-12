namespace EasyExtractCrossPlatform.Models;

/// <summary>
///     Returned by the package preview dialog when the user asks to extract
///     only the checked assets. The keys are the tar asset directory names
///     (asset GUIDs), which the extraction service filters on.
/// </summary>
public sealed record PackagePreviewExtractionRequest(
    string PackagePath,
    IReadOnlyCollection<string> AssetKeys);
