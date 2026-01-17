using System.Collections.Generic;

namespace EasyExtract.Core.Models;

public sealed record UnityPackageExtractionResult(
    string PackagePath,
    string OutputDirectory,
    int AssetsExtracted,
    IReadOnlyList<string> ExtractedFiles)
{
    // HakuAPI Requirement: Computed Property for Result Size
    // For now we default to 0, logic to populate this will be in the Service.
    public long TotalSize { get; init; } 
}
