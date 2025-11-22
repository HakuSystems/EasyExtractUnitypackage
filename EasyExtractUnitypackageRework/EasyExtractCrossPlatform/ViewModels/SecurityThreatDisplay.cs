namespace EasyExtractCrossPlatform.ViewModels;

public sealed class SecurityThreatDisplay
{
    private SecurityThreatDisplay(
        string title,
        string description,
        MaliciousThreatSeverity severity,
        IReadOnlyList<string> matches)
    {
        Title = title;
        Description = description;
        Severity = severity;
        Matches = matches;
    }

    public string Title { get; }

    public string Description { get; }

    public MaliciousThreatSeverity Severity { get; }

    public IReadOnlyList<string> Matches { get; }

    public bool HasMatches => Matches.Count > 0;

    public string SeverityLabel => Severity switch
    {
        MaliciousThreatSeverity.High => "HIGH",
        MaliciousThreatSeverity.Medium => "MEDIUM",
        _ => "LOW"
    };

    public static SecurityThreatDisplay FromThreat(MaliciousThreat threat)
    {
        if (threat is null)
            throw new ArgumentNullException(nameof(threat));

        var matches = threat.Matches is { Count: > 0 }
            ? threat.Matches
                .Select(match => $"{match.FilePath}: {match.Snippet}")
                .Take(6)
                .ToList()
            : new List<string>();

        return new SecurityThreatDisplay(
            ResolveThreatTitle(threat.Type),
            threat.Description,
            threat.Severity,
            matches);
    }

    private static string ResolveThreatTitle(MaliciousThreatType type)
    {
        return type switch
        {
            MaliciousThreatType.DiscordWebhook => "Discord webhook detected",
            MaliciousThreatType.UnsafeLinks => "Unsafe links detected",
            MaliciousThreatType.SuspiciousCodePatterns => "Suspicious code patterns",
            _ => type.ToString()
        };
    }
}