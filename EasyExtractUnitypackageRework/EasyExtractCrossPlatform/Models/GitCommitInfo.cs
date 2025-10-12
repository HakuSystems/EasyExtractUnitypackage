using System;
using System.Text.RegularExpressions;

namespace EasyExtractCrossPlatform.Models;

public record GitCommitInfo(
    string Sha,
    string Title,
    string? Description,
    string AuthorName,
    DateTimeOffset CommitDate,
    string CommitUrl)
{
    private static readonly Regex CategoryPattern = new(
        @"^(?<type>[a-z]+)(?:\((?<scope>[^)]+)\))?:\s*",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private (string CategoryKey, string DisplayName, string TitleRemainder)? _categoryCache;
    private string? _normalizedDescription;

    public bool HasDescription => !string.IsNullOrWhiteSpace(GetNormalizedDescription());

    public string CategoryKey => GetCategoryParts().CategoryKey;

    public string CategoryDisplayName => GetCategoryParts().DisplayName;

    public string TitleWithoutCategory => GetCategoryParts().TitleRemainder;

    public string TitleMarkdown
    {
        get
        {
            var content = TitleWithoutCategory.ReplaceLineEndings("\n").Trim();
            if (string.IsNullOrEmpty(content))
                content = (Title ?? string.Empty).Trim();

            return string.IsNullOrEmpty(content) ? string.Empty : $"**{content}**";
        }
    }

    public string? DescriptionMarkdown => GetNormalizedDescription();

    private (string CategoryKey, string DisplayName, string TitleRemainder) GetCategoryParts()
    {
        if (_categoryCache.HasValue)
            return _categoryCache.Value;

        var titleValue = Title ?? string.Empty;
        var match = CategoryPattern.Match(titleValue);

        if (!match.Success)
        {
            var fallback = ("other", "OTHER", titleValue.Trim());
            _categoryCache = fallback;
            return fallback;
        }

        var type = match.Groups["type"].Value;
        var scope = match.Groups["scope"].Success ? match.Groups["scope"].Value : null;

        var keyType = type.ToLowerInvariant();
        var displayType = keyType.ToUpperInvariant();

        var categoryKey = scope is { Length: > 0 }
            ? $"{keyType}({scope.ToLowerInvariant()})"
            : keyType;

        var displayName = scope is { Length: > 0 }
            ? $"{displayType} ({scope})"
            : displayType;

        var remainder = titleValue[match.Length..].TrimStart();
        if (string.IsNullOrWhiteSpace(remainder))
            remainder = titleValue.Trim();

        var result = (categoryKey, displayName, remainder);
        _categoryCache = result;
        return result;
    }

    private string? GetNormalizedDescription()
    {
        if (_normalizedDescription is not null)
            return _normalizedDescription.Length == 0 ? null : _normalizedDescription;

        if (string.IsNullOrWhiteSpace(Description))
        {
            _normalizedDescription = string.Empty;
            return null;
        }

        var normalized = Description.ReplaceLineEndings("\n").Trim();
        if (normalized.Length == 0)
        {
            _normalizedDescription = string.Empty;
            return null;
        }

        _normalizedDescription = normalized;
        return _normalizedDescription;
    }
}