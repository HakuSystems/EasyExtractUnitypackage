using EasyExtract.Core.Utilities;

namespace EasyExtract.Core.Services;

public sealed partial class UnityPackageExtractionService
{
    private static string NormalizeOutputDirectory(string directory)
    {
        var full = Path.GetFullPath(directory);
        if (!Path.EndsInDirectorySeparator(full))
            full += Path.DirectorySeparatorChar;
        return full;
    }


    private static void EnsurePathIsUnderRoot(string normalizedRoot, string candidatePath, string correlationId,
        IEasyExtractLogger logger)
    {
        var candidate = Path.GetFullPath(candidatePath);
        if (!candidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogError(
                $"Path traversal detected | candidate='{candidate}' | root='{normalizedRoot}' | correlationId={correlationId}");
            throw new InvalidDataException(
                $"Extraction aborted. Asset path '{candidate}' points outside of '{normalizedRoot}'.");
        }
    }


    private static string EnsureUniqueRelativePath(
        string relativePath,
        HashSet<string> usedRelativePaths,
        Dictionary<string, int> duplicateSuffixCounters,
        bool allowSuffixes,
        string correlationId,
        IEasyExtractLogger logger)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return relativePath;

        if (usedRelativePaths.Add(relativePath) || !allowSuffixes)
            return relativePath;

        var directory = Path.GetDirectoryName(relativePath);
        var fileName = Path.GetFileName(relativePath);

        if (string.IsNullOrWhiteSpace(fileName)) fileName = "Asset";

        var baseName = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);

        if (string.IsNullOrWhiteSpace(baseName))
        {
            baseName = fileName;
            extension = string.Empty;
        }

        var duplicateKey = string.IsNullOrWhiteSpace(directory)
            ? fileName
            : $"{directory}/{fileName}";

        if (!duplicateSuffixCounters.TryGetValue(duplicateKey, out var counter))
            counter = 1;

        while (true)
        {
            var suffixedName = $"{baseName} ({counter}){extension}";
            var candidatePath = string.IsNullOrWhiteSpace(directory)
                ? suffixedName
                : Path.Combine(directory, suffixedName);

            if (usedRelativePaths.Add(candidatePath))
            {
                duplicateSuffixCounters[duplicateKey] = counter + 1;
                logger.LogInformation(
                    $"Duplicate path resolved | original='{relativePath}' | unique='{candidatePath}' | suffix={counter} | correlationId={correlationId}");
                return candidatePath;
            }

            counter++;
        }
    }


    private static string SanitizePathSegment(string? segment)
    {
        if (string.IsNullOrWhiteSpace(segment))
            return "Other";

        var filtered = segment
            .Where(static c =>
                c != Path.DirectorySeparatorChar &&
                c != Path.AltDirectorySeparatorChar)
            .Where(c => !InvalidFileNameCharacters.Contains(c))
            .ToArray();

        var sanitized = new string(filtered).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "Other" : sanitized;
    }


    private static PathNormalizationResult NormalizeRelativePath(string? input, string correlationId,
        IEasyExtractLogger logger)
    {
        if (string.IsNullOrWhiteSpace(input))
            return new PathNormalizationResult(string.Empty, string.Empty, EmptySegmentNormalizations);

        var sanitized = input.Replace('\\', '/')
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", string.Empty, StringComparison.Ordinal)
            .Trim();

        if (string.IsNullOrWhiteSpace(sanitized))
            return new PathNormalizationResult(string.Empty, string.Empty, EmptySegmentNormalizations);

        var segments = sanitized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
            return new PathNormalizationResult(string.Empty, string.Empty, EmptySegmentNormalizations);

        var originalSegments = new List<string>(segments.Length);
        var normalizedSegments = new List<string>(segments.Length);
        var hadDotDotSegments = false;
        var filteredSegments = 0;

        foreach (var segment in segments)
        {
            if (segment is "." or "..")
            {
                hadDotDotSegments = true;
                continue;
            }

            var trimmed = segment.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
                continue;

            var filtered = new string(trimmed.Where(c => !InvalidFileNameCharacters.Contains(c)).ToArray());
            filtered = filtered.Trim();
            if (string.IsNullOrWhiteSpace(filtered))
            {
                filteredSegments++;
                continue;
            }

            originalSegments.Add(filtered);
            var normalizedSegment = FileExtensionNormalizer.Normalize(filtered);
            normalizedSegments.Add(normalizedSegment);
        }

        if (originalSegments.Count == 0)
        {
            logger.LogWarning(
                $"Path normalization resulted in empty path | original='{input}' | hadDotDot={hadDotDotSegments} | filteredCount={filteredSegments} | correlationId={correlationId}");
            return new PathNormalizationResult(string.Empty, string.Empty, EmptySegmentNormalizations);
        }

        var segmentPairs = new PathSegmentNormalization[originalSegments.Count];
        for (var i = 0; i < segmentPairs.Length; i++)
            segmentPairs[i] = new PathSegmentNormalization(originalSegments[i], normalizedSegments[i]);

        var originalPath = Path.Combine(originalSegments.ToArray());
        var normalizedPath = Path.Combine(normalizedSegments.ToArray());

        if (!string.Equals(originalPath, normalizedPath, StringComparison.Ordinal))
            logger.LogInformation(
                $"Path normalized | original='{originalPath}' | normalized='{normalizedPath}' | correlationId={correlationId}");

        return new PathNormalizationResult(normalizedPath, originalPath, segmentPairs);
    }

    private static bool TryGetAssetPaths(
        UnityPackageAssetState state,
        string outputDirectory,
        string normalizedOutputDirectory,
        bool organizeByCategories,
        HashSet<string> generatedRelativePaths,
        Dictionary<string, int> duplicateSuffixCounters,
        HashSet<string> directoriesToCleanup,
        string correlationId,
        IEasyExtractLogger logger,
        out string targetPath,
        out string? metaPath,
        out string? previewPath)
    {
        targetPath = string.Empty;
        metaPath = null;
        previewPath = null;

        if (state.RelativePath is null || state.Asset is not { HasContent: true })
            return false;

        var relativeOutputPath = ResolveOutputRelativePath(state, organizeByCategories);

        if (string.IsNullOrWhiteSpace(relativeOutputPath))
            return false;

        var originalPath = relativeOutputPath;
        relativeOutputPath = EnsureUniqueRelativePath(
            relativeOutputPath,
            generatedRelativePaths,
            duplicateSuffixCounters,
            organizeByCategories,
            correlationId,
            logger);

        if (!string.Equals(originalPath, relativeOutputPath, StringComparison.Ordinal))
            logger.LogInformation(
                $"Path renamed for uniqueness | original='{originalPath}' | unique='{relativeOutputPath}' | correlationId={correlationId}");

        targetPath = Path.Combine(outputDirectory, relativeOutputPath);
        metaPath = state.Meta is { HasContent: true } ? $"{targetPath}.meta" : null;
        previewPath = state.Preview is { HasContent: true } ? $"{targetPath}.preview.png" : null;

        try
        {
            EnsurePathIsUnderRoot(normalizedOutputDirectory, targetPath, correlationId, logger);
            if (metaPath is not null)
                EnsurePathIsUnderRoot(normalizedOutputDirectory, metaPath, correlationId, logger);
            if (previewPath is not null)
                EnsurePathIsUnderRoot(normalizedOutputDirectory, previewPath, correlationId, logger);
        }
        catch (InvalidDataException ex)
        {
            logger.LogError(
                $"Path validation failed for asset | relativePath='{state.RelativePath}' | correlationId={correlationId}",
                ex);
            throw;
        }

        TrackCorruptedDirectories(outputDirectory, state, directoriesToCleanup);
        return true;
    }

    private static string? ResolveOutputRelativePath(
        UnityPackageAssetState state,
        bool organizeByCategories)
    {
        var relativePath = state.RelativePath;
        if (string.IsNullOrWhiteSpace(relativePath))
            return null;

        if (!organizeByCategories)
        {
            var normalizedSegments = state.PathNormalizations?
                .Select(segment => segment.Normalized)
                .Where(segment => !string.IsNullOrWhiteSpace(segment))
                .ToArray();

            if (normalizedSegments is { Length: > 0 })
                return Path.Combine(normalizedSegments);

            return relativePath;
        }

        var assetSize = state.Asset?.Length ?? 0L;
        var category = UnityAssetClassification.ResolveCategory(
            state.OriginalRelativePath ?? relativePath,
            assetSize,
            state.Asset?.HasContent ?? false);
        var categorySegment = SanitizePathSegment(category);

        var fileName = Path.GetFileName(relativePath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            var fallbackSegment = state.PathNormalizations?
                .Select(segment => segment.Normalized)
                .LastOrDefault(segment => !string.IsNullOrWhiteSpace(segment));
            if (!string.IsNullOrWhiteSpace(fallbackSegment))
            {
                fileName = fallbackSegment;
            }
            else
            {
                var originalFileName = Path.GetFileName(state.OriginalRelativePath ?? string.Empty);
                if (!string.IsNullOrWhiteSpace(originalFileName))
                    fileName = SanitizePathSegment(originalFileName);
            }
        }

        if (string.IsNullOrWhiteSpace(fileName))
            fileName = "Asset";

        return Path.Combine(categorySegment, fileName);
    }
}