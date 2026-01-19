using System;
using System.Collections.Generic;

namespace EasyExtract.Core.Models;

public enum MaliciousThreatSeverity
{
    Low,
    Medium,
    High
}

public enum MaliciousThreatType
{
    DiscordWebhook,
    UnsafeLinks,
    SuspiciousCodePatterns
}

public sealed record MaliciousThreatMatch(string FilePath, string Snippet);

public sealed record MaliciousThreat(
    MaliciousThreatType Type,
    MaliciousThreatSeverity Severity,
    string Description,
    IReadOnlyList<MaliciousThreatMatch> Matches);

public sealed record MaliciousCodeScanResult(
    string PackagePath,
    bool IsMalicious,
    IReadOnlyList<MaliciousThreat> Threats,
    DateTimeOffset ScanTimestamp);
