using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using EasyExtractCrossPlatform.Models;

namespace EasyExtractCrossPlatform.ViewModels;

public class HistoryViewModel
{
    private const int DailyActivityRangeDays = 10;
    private const double BarGraphHeight = 140d;
    private const double VelocityGraphHeight = 100d;

    public HistoryViewModel(IReadOnlyList<HistoryEntry>? entries)
    {
        var sanitized = entries?
            .Where(entry => entry is not null)
            .OrderBy(entry => entry.AddedUtc)
            .ToList() ?? new List<HistoryEntry>();

        HasEntries = sanitized.Count > 0;
        SummaryCards = BuildSummaryCards(sanitized);
        DailyActivity = BuildDailyActivity(sanitized);
        VelocityPoints = BuildVelocityPoints(sanitized);
        HasVelocityPoints = VelocityPoints.Count > 0;
        RecentEntries = BuildRecentEntries(sanitized);
        WindowSubtitle = BuildSubtitle(sanitized);

        EmptyStateHeadline = "History is empty";
        EmptyStateDescription = "Queue and extract .unitypackage files to unlock usage insights.";
    }

    public IReadOnlyList<HistorySummaryCard> SummaryCards { get; }
    public IReadOnlyList<HistoryActivityPoint> DailyActivity { get; }
    public IReadOnlyList<HistoryVelocityPoint> VelocityPoints { get; }
    public IReadOnlyList<HistoryEntryDisplay> RecentEntries { get; }
    public string WindowSubtitle { get; }
    public bool HasEntries { get; }
    public bool HasVelocityPoints { get; }
    public string EmptyStateHeadline { get; }
    public string EmptyStateDescription { get; }

    private static IReadOnlyList<HistorySummaryCard> BuildSummaryCards(IReadOnlyList<HistoryEntry> entries)
    {
        var extracted = entries.Where(entry => entry.WasExtracted).ToList();
        var totalPackages = entries.Count;
        var extractedCount = extracted.Count;
        var successRate = totalPackages == 0
            ? 0
            : (double)extractedCount / totalPackages * 100d;

        var totalAssets = extracted.Sum(entry => Math.Max(0, entry.AssetsExtracted));
        var totalBytesProcessed = entries.Sum(entry =>
            entry.ExtractedBytes > 0 ? entry.ExtractedBytes : entry.FileSizeBytes);

        var durations = extracted
            .Select(entry => entry.ExtractionDurationMs)
            .Where(duration => duration > 0)
            .ToList();
        var averageDuration = durations.Count > 0
            ? durations.Average()
            : 0d;

        var largestPackage = entries
            .OrderByDescending(entry => entry.FileSizeBytes)
            .FirstOrDefault();

        var mostRecentExtraction = extracted
            .OrderByDescending(entry => entry.ExtractedUtc)
            .FirstOrDefault();

        return new List<HistorySummaryCard>
        {
            new("Tracked packages",
                totalPackages.ToString("N0", CultureInfo.InvariantCulture),
                $"{extractedCount:N0} extracted • {successRate:0.#}%",
                "primary"),
            new("Data processed",
                FormatBytes(totalBytesProcessed),
                $"{totalAssets:N0} assets extracted",
                "accent"),
            new("Average extraction time",
                FormatDuration(averageDuration),
                mostRecentExtraction is not null
                    ? $"Last run {FormatRelativeTime(mostRecentExtraction.ExtractedUtc)}"
                    : "No extractions yet",
                "muted"),
            new("Largest package",
                FormatBytes(largestPackage?.FileSizeBytes ?? 0),
                largestPackage?.FileName ?? "—",
                "outline")
        };
    }

    private static IReadOnlyList<HistoryActivityPoint> BuildDailyActivity(IReadOnlyList<HistoryEntry> entries)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var buckets = new List<HistoryActivityBucket>();
        for (var offset = DailyActivityRangeDays - 1; offset >= 0; offset--)
        {
            var day = today.AddDays(-offset);
            buckets.Add(new HistoryActivityBucket(day));
        }

        foreach (var entry in entries)
        {
            var addedDay = DateOnly.FromDateTime(entry.AddedUtc.Date);
            var addedBucket = buckets.FirstOrDefault(bucket => bucket.Day == addedDay);
            addedBucket?.IncrementQueued();

            if (entry.ExtractedUtc is not null)
            {
                var extractedDay = DateOnly.FromDateTime(entry.ExtractedUtc.Value.Date);
                var extractedBucket = buckets.FirstOrDefault(bucket => bucket.Day == extractedDay);
                extractedBucket?.IncrementExtracted();
            }
        }

        var peakValue = buckets.Max(bucket => bucket.TotalCount);
        peakValue = Math.Max(1, peakValue);

        return buckets.Select(bucket =>
        {
            var queuedHeight = bucket.QueuedCount / peakValue * BarGraphHeight;
            var extractedHeight = bucket.ExtractedCount / peakValue * BarGraphHeight;
            var label = bucket.Day == today ? "Today" : bucket.Day.ToString("ddd", CultureInfo.InvariantCulture);
            return new HistoryActivityPoint(
                label,
                bucket.Day.ToString("MMM d", CultureInfo.InvariantCulture),
                bucket.QueuedCount,
                bucket.ExtractedCount,
                Math.Max(6, queuedHeight),
                Math.Max(6, extractedHeight),
                bucket.Day == today);
        }).ToList();
    }

    private static IReadOnlyList<HistoryVelocityPoint> BuildVelocityPoints(IReadOnlyList<HistoryEntry> entries)
    {
        var extractedEntries = entries
            .Where(entry => entry.WasExtracted && entry.ExtractedUtc is not null)
            .OrderBy(entry => entry.ExtractedUtc)
            .ToList();

        if (extractedEntries.Count == 0)
            return Array.Empty<HistoryVelocityPoint>();

        const int sampleSize = 12;
        var sample = extractedEntries.Count > sampleSize
            ? extractedEntries.Skip(extractedEntries.Count - sampleSize).ToList()
            : extractedEntries;

        var values = sample
            .Select(entry => entry.AssetsExtracted > 0
                ? entry.AssetsExtracted
                : entry.FileSizeBytes > 0
                    ? (int)Math.Max(1, entry.FileSizeBytes / (1024 * 1024))
                    : 1)
            .ToList();

        var maxValue = Math.Max(1, values.Max());

        var points = new List<HistoryVelocityPoint>();
        for (var index = 0; index < sample.Count; index++)
        {
            var entry = sample[index];
            var value = values[index];
            var height = value / maxValue * VelocityGraphHeight;
            points.Add(new HistoryVelocityPoint(
                $"{entry.FileName}",
                $"{value:N0} {(entry.AssetsExtracted > 0 ? "assets" : "MB")}",
                entry.ExtractedUtc is not null
                    ? entry.ExtractedUtc.Value.ToLocalTime().ToString("MMM d • h:mm tt", CultureInfo.CurrentCulture)
                    : "Pending",
                Math.Max(8, height)));
        }

        return points;
    }

    private static IReadOnlyList<HistoryEntryDisplay> BuildRecentEntries(IReadOnlyList<HistoryEntry> entries)
    {
        return entries
            .OrderByDescending(entry => entry.LastSeenUtc)
            .Take(12)
            .Select(entry =>
            {
                var statusLabel = entry.WasExtracted ? "Extracted" : "Queued";
                var statusTag = entry.WasExtracted ? "success" : "muted";
                var timestamp = entry.ExtractedUtc ?? entry.LastSeenUtc;
                var detail = $"{FormatRelativeTime(timestamp)} • {FormatBytes(entry.FileSizeBytes)}";
                return new HistoryEntryDisplay(
                    entry.FileName,
                    detail,
                    statusLabel,
                    statusTag,
                    entry.OutputDirectory,
                    entry.AssetsExtracted > 0
                        ? $"{entry.AssetsExtracted:N0} assets"
                        : entry.WasExtracted
                            ? "Extraction complete"
                            : "Waiting in queue");
            })
            .ToList();
    }

    private static string BuildSubtitle(IReadOnlyList<HistoryEntry> entries)
    {
        if (entries.Count == 0)
            return "No packages have been analyzed yet.";

        var firstTimestamp = entries.Min(entry => entry.AddedUtc).ToLocalTime();
        var lastTimestamp = entries.Max(entry => entry.LastSeenUtc).ToLocalTime();

        return $"Tracking since {firstTimestamp:MMM d} • Last activity {FormatRelativeTime(lastTimestamp)}";
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0)
            return "0 B";

        var units = new[] { "B", "KB", "MB", "GB", "TB" };
        var order = (int)Math.Floor(Math.Log(bytes, 1024));
        order = Math.Clamp(order, 0, units.Length - 1);
        var value = bytes / Math.Pow(1024, order);
        return $"{value:0.#} {units[order]}";
    }

    private static string FormatDuration(double milliseconds)
    {
        if (milliseconds <= 0)
            return "—";

        if (milliseconds < 1000)
            return $"{milliseconds:0} ms";

        var seconds = milliseconds / 1000d;
        if (seconds < 60)
            return $"{seconds:0.0}s";

        var minutes = seconds / 60d;
        return minutes < 60 ? $"{minutes:0.#} min" : $"{minutes / 60d:0.#} h";
    }

    private static string FormatRelativeTime(DateTimeOffset? timestamp)
    {
        if (timestamp is null)
            return "—";

        var local = timestamp.Value.ToLocalTime();
        var delta = DateTimeOffset.Now - local;

        return delta.TotalMinutes switch
        {
            <= 1 => "just now",
            < 60 => $"{delta.TotalMinutes:0} min ago",
            < 1440 => $"{delta.TotalHours:0.#} h ago",
            _ => local.ToString("MMM d, h:mm tt", CultureInfo.CurrentCulture)
        };
    }

    private sealed class HistoryActivityBucket
    {
        public HistoryActivityBucket(DateOnly day)
        {
            Day = day;
        }

        public DateOnly Day { get; }
        public double QueuedCount { get; private set; }
        public double ExtractedCount { get; private set; }
        public double TotalCount => Math.Max(QueuedCount, ExtractedCount);

        public void IncrementQueued()
        {
            QueuedCount++;
        }

        public void IncrementExtracted()
        {
            ExtractedCount++;
        }
    }
}

public sealed record HistorySummaryCard(
    string Title,
    string Value,
    string Description,
    string Accent);

public sealed record HistoryActivityPoint(
    string Label,
    string SubLabel,
    double QueuedCount,
    double ExtractedCount,
    double QueuedHeight,
    double ExtractedHeight,
    bool IsToday);

public sealed record HistoryVelocityPoint(
    string Label,
    string Tooltip,
    string Timestamp,
    double Height);

public sealed record HistoryEntryDisplay(
    string FileName,
    string Details,
    string StatusLabel,
    string StatusTag,
    string OutputDirectory,
    string Secondary);