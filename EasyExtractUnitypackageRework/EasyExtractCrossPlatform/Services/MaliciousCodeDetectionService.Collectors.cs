namespace EasyExtractCrossPlatform.Services;

public sealed partial class MaliciousCodeDetectionService
{
    private sealed class ScanStatistics
    {
        public int TarEntriesRead { get; set; }
        public int PathComponentsSeen { get; set; }
        public int AssetComponentsSeen { get; set; }
        public int AssetsAnalyzed { get; set; }
        public int AssetsSkippedByExtension { get; set; }
        public int AssetsSkippedBinary { get; set; }
        public int AssetsSkippedMissingPath { get; set; }
        public int AssetsSkippedOversize { get; set; }
        public int AssetsSkippedMissingStream { get; set; }
        public long TotalBytesAnalyzed { get; set; }
        public int ThreatCount { get; set; }

        public string ToPerformanceDetails()
        {
            return
                $"entries={TarEntriesRead}|assets={AssetComponentsSeen}|analyzed={AssetsAnalyzed}|skipExt={AssetsSkippedByExtension}|skipBinary={AssetsSkippedBinary}|skipMissingPath={AssetsSkippedMissingPath}|skipSize={AssetsSkippedOversize}|skipStream={AssetsSkippedMissingStream}|threats={ThreatCount}";
        }
    }

    private sealed record CachedScanResult(MaliciousCodeScanResult Result, DateTimeOffset Timestamp);

    private sealed class AssetSecurityState
    {
        public string? RelativePath { get; set; }
        public byte[]? PendingAssetData { get; set; }
    }

    private sealed class MaliciousThreatCollector
    {
        private readonly Dictionary<MaliciousThreatType, ThreatAccumulator> _accumulators = new();

        public void AddMatches(
            MaliciousThreatType type,
            MaliciousThreatSeverity severity,
            string description,
            string filePath,
            IEnumerable<string> matches)
        {
            if (!matches.Any())
                return;

            if (!_accumulators.TryGetValue(type, out var accumulator))
            {
                accumulator = new ThreatAccumulator(type, severity, description);
                _accumulators[type] = accumulator;
            }

            accumulator.AddMatches(filePath, matches);
        }

        public List<MaliciousThreat> BuildResults()
        {
            var results = new List<MaliciousThreat>();
            foreach (var accumulator in _accumulators.Values)
            {
                if (!accumulator.HasMatches)
                    continue;

                results.Add(new MaliciousThreat(
                    accumulator.Type,
                    accumulator.Severity,
                    accumulator.Description,
                    accumulator.ToMatches()));
            }

            return results;
        }
    }

    private sealed class ThreatAccumulator
    {
        private const int MaxMatchesPerThreat = 50;

        private readonly Dictionary<string, HashSet<string>> _matchesByFile =
            new(StringComparer.OrdinalIgnoreCase);

        private int _totalMatches;

        public ThreatAccumulator(
            MaliciousThreatType type,
            MaliciousThreatSeverity severity,
            string description)
        {
            Type = type;
            Severity = severity;
            Description = description;
        }

        public MaliciousThreatType Type { get; }
        public MaliciousThreatSeverity Severity { get; }
        public string Description { get; }

        public bool HasMatches => _totalMatches > 0;

        public void AddMatches(string filePath, IEnumerable<string> matches)
        {
            if (_totalMatches >= MaxMatchesPerThreat)
                return;

            var path = NormalizeAssetPath(filePath);
            if (string.IsNullOrWhiteSpace(path))
                path = "Unknown asset";

            if (!_matchesByFile.TryGetValue(path, out var set))
            {
                set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _matchesByFile[path] = set;
            }

            foreach (var match in matches)
            {
                if (_totalMatches >= MaxMatchesPerThreat)
                    break;

                if (string.IsNullOrWhiteSpace(match))
                    continue;

                if (set.Add(match))
                    _totalMatches++;
            }
        }

        public IReadOnlyList<MaliciousThreatMatch> ToMatches()
        {
            var matches = new List<MaliciousThreatMatch>(_totalMatches);
            foreach (var kvp in _matchesByFile.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
            {
                var snippets = kvp.Value.ToList();
                snippets.Sort(StringComparer.OrdinalIgnoreCase);
                foreach (var snippet in snippets)
                    matches.Add(new MaliciousThreatMatch(kvp.Key, snippet));
            }

            return matches;
        }
    }
}