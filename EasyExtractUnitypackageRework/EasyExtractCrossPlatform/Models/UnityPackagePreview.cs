using System;
using System.Collections.Generic;

namespace EasyExtractCrossPlatform.Models;

public sealed record UnityPackagePreviewResult(
    string PackagePath,
    string PackageName,
    long PackageSizeBytes,
    DateTimeOffset? LastModifiedUtc,
    long TotalAssetSizeBytes,
    IReadOnlyList<UnityPackagePreviewAsset> Assets,
    IReadOnlyCollection<string> DirectoriesToPrune);

public sealed record UnityPackagePreviewAsset(
    string RelativePath,
    long AssetSizeBytes,
    bool HasMetaFile,
    byte[]? PreviewImageData,
    byte[]? AssetData,
    bool IsAssetDataTruncated);