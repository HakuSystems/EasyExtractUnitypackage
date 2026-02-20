namespace EasyExtract.Core.Models;

public sealed record UnityPackageExtractionOptions(
    bool OrganizeByCategories,
    string? TemporaryDirectory,
    UnityPackageExtractionLimits? Limits = null);