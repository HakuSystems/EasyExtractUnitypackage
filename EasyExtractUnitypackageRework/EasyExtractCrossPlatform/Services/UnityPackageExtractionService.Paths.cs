using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EasyExtractCrossPlatform.Models;
using EasyExtractCrossPlatform.Utilities;

namespace EasyExtractCrossPlatform.Services;

public sealed partial class UnityPackageExtractionService
{
    private static string NormalizeOutputDirectory(string directory)
    {
        var full = Path.GetFullPath(directory);
        if (!Path.EndsInDirectorySeparator(full))
            full += Path.DirectorySeparatorChar;
        return full;
    }


    private static void EnsurePathIsUnderRoot(string normalizedRoot, string candidatePath, string correlationId)
    {
        var candidate = Path.GetFullPath(candidatePath);
        if (!candidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            LoggingService.LogError(
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
        string correlationId)
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
                LoggingService.LogInformation(
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


    private static PathNormalizationResult NormalizeRelativePath(string? input, string correlationId)
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
            LoggingService.LogWarning(
                $"Path normalization resulted in empty path | original='{input}' | hadDotDot={hadDotDotSegments} | filteredCount={filteredSegments} | correlationId={correlationId}");
            return new PathNormalizationResult(string.Empty, string.Empty, EmptySegmentNormalizations);
        }

        var segmentPairs = new PathSegmentNormalization[originalSegments.Count];
        for (var i = 0; i < segmentPairs.Length; i++)
            segmentPairs[i] = new PathSegmentNormalization(originalSegments[i], normalizedSegments[i]);

        var originalPath = Path.Combine(originalSegments.ToArray());
        var normalizedPath = Path.Combine(normalizedSegments.ToArray());

        if (!string.Equals(originalPath, normalizedPath, StringComparison.Ordinal))
            LoggingService.LogInformation(
                $"Path normalized | original='{originalPath}' | normalized='{normalizedPath}' | correlationId={correlationId}");

        return new PathNormalizationResult(normalizedPath, originalPath, segmentPairs);
    }


    private readonly record struct PathSegmentNormalization(string Original, string Normalized);


    private readonly record struct PathNormalizationResult(
        string NormalizedPath,
        string OriginalPath,
        IReadOnlyList<PathSegmentNormalization> Segments);

}
