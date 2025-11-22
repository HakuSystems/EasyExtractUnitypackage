namespace EasyExtractCrossPlatform.Services;

internal sealed class SpotlightSearchService : IEverythingSearchService
{
    private static readonly string? SpotlightPath = SearchUtilities.TryResolveExecutable("mdfind");

    private readonly SemaphoreSlim _queryGate = new(1, 1);
    private string _availabilityHint = "Spotlight search has not been initialized.";
    private int _lastExcludedResultCount;

    public int LastExcludedResultCount => Volatile.Read(ref _lastExcludedResultCount);

    public string AvailabilityHint => Volatile.Read(ref _availabilityHint);

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
    {
        var available = SpotlightPath is not null;
        Volatile.Write(ref _availabilityHint, available
            ? "Using macOS Spotlight (mdfind) for .unitypackage search."
            : "Spotlight command 'mdfind' is unavailable. Install the macOS command line tools or ensure Spotlight indexing is enabled.");
        LoggingService.LogInformation($"Spotlight availability check: {(available ? "available" : "unavailable")}.");
        return Task.FromResult(available);
    }

    public async Task<IReadOnlyList<EverythingSearchResult>> SearchAsync(
        string query,
        int maxResults,
        CancellationToken cancellationToken)
    {
        LoggingService.LogInformation($"Spotlight search requested. Query='{query}', maxResults={maxResults}.");

        if (!OperatingSystem.IsMacOS())
        {
            LoggingService.LogInformation("Spotlight search skipped for non-macOS platform.");
            return Array.Empty<EverythingSearchResult>();
        }

        if (SpotlightPath is null)
            throw new InvalidOperationException(
                "Spotlight search is unavailable because the 'mdfind' command could not be resolved.");

        if (string.IsNullOrWhiteSpace(query))
        {
            LoggingService.LogInformation("Spotlight search skipped because the query was empty.");
            return Array.Empty<EverythingSearchResult>();
        }

        maxResults = Math.Clamp(maxResults, 1, 2000);
        LoggingService.LogInformation($"Spotlight search normalized maxResults={maxResults}.");

        var stopwatch = Stopwatch.StartNew();

        var entered = false;
        try
        {
            await _queryGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            entered = true;

            var execution = await Task.Run(() =>
                    ExecuteSearchInternal(SpotlightPath, query, maxResults, cancellationToken),
                cancellationToken).ConfigureAwait(false);

            Volatile.Write(ref _lastExcludedResultCount, execution.FilteredCount);
            stopwatch.Stop();
            LoggingService.LogInformation(
                $"Spotlight search completed in {stopwatch.Elapsed.TotalMilliseconds:F0} ms. Results={execution.Results.Count}, excluded={execution.FilteredCount}.");
            return execution.Results;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            stopwatch.Stop();
            LoggingService.LogError("Spotlight search failed.", ex);
            throw;
        }
        finally
        {
            stopwatch.Stop();
            if (entered)
                _queryGate.Release();
        }
    }

    private static (List<EverythingSearchResult> Results, int FilteredCount) ExecuteSearchInternal(
        string commandPath,
        string query,
        int maxResults,
        CancellationToken cancellationToken)
    {
        var spotlightQuery =
            $"kMDItemFSName == '*{EscapeSpotlightQuery(query)}*'cd && kMDItemFSName == '*.unitypackage'c";

        var processStartInfo = new ProcessStartInfo
        {
            FileName = commandPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        processStartInfo.ArgumentList.Add(spotlightQuery);

        LoggingService.LogInformation($"Executing Spotlight command: {commandPath} {spotlightQuery}");

        using var process = Process.Start(processStartInfo) ??
                            throw new InvalidOperationException("Unable to start the Spotlight search process.");

        using var cancellationRegistration = cancellationToken.Register(() => { TryTerminate(process); });

        var results = new List<EverythingSearchResult>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var excluded = 0;

        while (!process.StandardOutput.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var rawLine = process.StandardOutput.ReadLine();
            if (rawLine is null)
                break;

            var candidate = rawLine.Trim();
            if (candidate.Length == 0)
                continue;

            if (!candidate.EndsWith(".unitypackage", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!seen.Add(candidate))
                continue;

            var result = SearchUtilities.BuildResultFromPath(candidate);
            if (result is null)
            {
                excluded++;
                continue;
            }

            results.Add(result);
            if (results.Count >= maxResults)
            {
                TryTerminate(process);
                break;
            }
        }

        process.WaitForExit();
        var errorOutput = process.StandardError.ReadToEnd();

        if (process.ExitCode != 0 && !cancellationToken.IsCancellationRequested)
        {
            var message = $"mdfind exited with code {process.ExitCode}: {errorOutput}";
            LoggingService.LogError($"Spotlight command failed. {message}");
            throw new InvalidOperationException(message);
        }

        LoggingService.LogInformation(
            $"Spotlight command completed with exit code {process.ExitCode}. Results={results.Count}, excluded={excluded}.");

        return (results, excluded);
    }

    private static string EscapeSpotlightQuery(string query)
    {
        return query
            .Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private static void TryTerminate(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(true);
        }
        catch
        {
            // Process already exited or could not be terminated. Ignore.
        }
    }
}