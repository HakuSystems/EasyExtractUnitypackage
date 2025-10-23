using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace EasyExtractCrossPlatform.Models;

public sealed record GitReleaseInfo(
    long Id,
    string? Name,
    string TagName,
    string Author,
    DateTimeOffset? PublishedAt,
    string? Body,
    string HtmlUrl,
    bool IsDraft,
    bool IsPrerelease,
    IReadOnlyList<GitReleaseAsset> Assets)
{
    private const int PreviewLength = 500;
    private string? _bodyWithoutSummaryMarkdown;

    private string? _summary;

    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? TagName : Name!;

    public string PublishedLabel => PublishedAt.HasValue
        ? PublishedAt.Value.ToLocalTime().ToString("MMMM d, yyyy", CultureInfo.CurrentCulture)
        : "Unpublished";

    public bool HasBody => !string.IsNullOrWhiteSpace(Body);

    public string? BodyMarkdown => HasBody ? Body : null;

    public string? BodyPreviewMarkdown
    {
        get
        {
            if (!HasBody)
                return null;

            var normalized = Body!.ReplaceLineEndings("\n").Trim();
            if (normalized.Length <= PreviewLength)
                return normalized;

            var truncated = normalized[..PreviewLength];
            var lastParagraphBreak = truncated.LastIndexOf("\n\n", StringComparison.Ordinal);
            if (lastParagraphBreak > PreviewLength * 0.5)
                truncated = truncated[..lastParagraphBreak];

            if (!truncated.EndsWith("...", StringComparison.Ordinal))
                truncated = $"{truncated.TrimEnd('.', ' ', '\n')}\u2026";

            return truncated;
        }
    }

    public bool HasAssets => Assets.Count > 0;

    public GitReleaseAsset? PrimaryAsset => Assets.FirstOrDefault();

    public bool HasPrimaryAsset => PrimaryAsset is not null;

    public bool HasBodyPreview => BodyPreviewMarkdown is not null;

    public bool HasSummary => !string.IsNullOrWhiteSpace(Summary);

    public bool HasBodyMarkdown => !string.IsNullOrWhiteSpace(BodyWithoutSummaryMarkdown);

    public string? PrimaryAssetName => PrimaryAsset?.Name;

    public string? PrimaryAssetDownloadUrl => PrimaryAsset?.BrowserDownloadUrl;

    public string? PrimaryAssetSizeLabel => PrimaryAsset?.SizeLabel;

    public string? Summary => _summary ??= ExtractSummaryLine();

    public string? BodyWithoutSummaryMarkdown =>
        _bodyWithoutSummaryMarkdown ??= ExtractBodyWithoutSummary();

    private string? ExtractSummaryLine()
    {
        if (string.IsNullOrWhiteSpace(Body))
            return null;

        var (summary, _) = SplitBody(Body);
        return summary;
    }

    private string? ExtractBodyWithoutSummary()
    {
        if (string.IsNullOrWhiteSpace(Body))
            return null;

        var (_, remainder) = SplitBody(Body);
        return remainder;
    }

    private static (string? Summary, string? Remainder) SplitBody(string body)
    {
        var normalizedBody = body.ReplaceLineEndings("\n");
        var lines = normalizedBody.Split('\n');

        string? summary = null;
        var firstContentIndex = lines.Length;
        string? candidate = null;
        var candidateIndex = lines.Length;

        for (var i = 0; i < lines.Length; i++)
        {
            var raw = lines[i];
            var trimmed = raw.Trim();

            if (trimmed.Length == 0)
                continue;

            var normalized = NormalizeSummaryLine(trimmed);

            if (candidate is null)
            {
                candidate = normalized;
                candidateIndex = i + 1;
            }

            if (IsStructuralMarkdownLine(trimmed))
                continue;

            summary = normalized;
            firstContentIndex = i + 1;
            break;
        }

        if (summary is null && candidate is not null)
        {
            summary = candidate;
            firstContentIndex = candidateIndex;
        }

        while (firstContentIndex < lines.Length && string.IsNullOrWhiteSpace(lines[firstContentIndex]))
            firstContentIndex++;

        var remainder = firstContentIndex < lines.Length
            ? string.Join("\n", lines[firstContentIndex..]).TrimEnd()
            : null;

        if (!string.IsNullOrWhiteSpace(remainder))
            remainder = AdjustHeadingLevels(remainder);

        return (summary, string.IsNullOrWhiteSpace(remainder) ? null : remainder);
    }

    private static string NormalizeSummaryLine(string value)
    {
        var trimmed = value.Trim();

        trimmed = trimmed.TrimStart('#').TrimStart();

        if (trimmed.StartsWith("* "))
            trimmed = trimmed[2..];
        else if (trimmed.StartsWith("- "))
            trimmed = trimmed[2..];
        else if (trimmed.StartsWith("> "))
            trimmed = trimmed[2..];

        return trimmed.Trim();
    }

    private static bool IsStructuralMarkdownLine(string value)
    {
        return value.StartsWith('#') ||
               value.StartsWith("- ") ||
               value.StartsWith("* ") ||
               value.StartsWith("> ");
    }

    private static string AdjustHeadingLevels(string value)
    {
        var lines = value.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var leadingWhitespaceCount = line.Length - line.TrimStart().Length;
            var trimmed = line.TrimStart();

            if (!trimmed.StartsWith("#", StringComparison.Ordinal))
                continue;

            var originalCount = 0;
            while (originalCount < trimmed.Length && trimmed[originalCount] == '#')
                originalCount++;

            var desiredCount = Math.Min(originalCount + 1, 6);
            var rest = originalCount < trimmed.Length ? trimmed[originalCount..] : string.Empty;
            var prefix = new string('#', desiredCount);
            var leading = leadingWhitespaceCount > 0 ? new string(' ', leadingWhitespaceCount) : string.Empty;
            lines[i] = $"{leading}{prefix}{rest}";
        }

        return string.Join("\n", lines);
    }
}

public sealed record GitReleaseAsset(
    long Id,
    string Name,
    long SizeBytes,
    string BrowserDownloadUrl,
    string? ContentType)
{
    public string SizeLabel => SizeBytes switch
    {
        < 1_024 => $"{SizeBytes} B",
        < 1_048_576 => $"{SizeBytes / 1024d:0.#} KB",
        < 1_073_741_824 => $"{SizeBytes / 1_048_576d:0.#} MB",
        _ => $"{SizeBytes / 1_073_741_824d:0.#} GB"
    };
}