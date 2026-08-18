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

    // AssetsExtracted alone cannot tell "nothing to do" from "nothing understood", so the
    // service classifies every run and callers branch on this instead of on the count.
    public UnityPackageExtractionOutcome Outcome { get; init; } = UnityPackageExtractionOutcome.Extracted;

    // Human-readable reason behind Outcome, shown to the user when nothing came out.
    public string? OutcomeDetail { get; init; }
}