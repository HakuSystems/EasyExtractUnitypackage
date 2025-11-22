namespace EasyExtractCrossPlatform.Services;

public interface IEverythingSearchService
{
    int LastExcludedResultCount { get; }

    string AvailabilityHint { get; }

    Task<IReadOnlyList<EverythingSearchResult>> SearchAsync(string query, int maxResults,
        CancellationToken cancellationToken);

    Task<bool> IsAvailableAsync(CancellationToken cancellationToken);
}

public sealed partial class EverythingSearchService : IEverythingSearchService
{
    private readonly SemaphoreSlim _queryGate = new(1, 1);
    private string _availabilityHint = "Everything search has not been initialized.";
    private int _lastExcludedResultCount;

    public int LastExcludedResultCount => Volatile.Read(ref _lastExcludedResultCount);

    public string AvailabilityHint => Volatile.Read(ref _availabilityHint);

    public async Task<IReadOnlyList<EverythingSearchResult>> SearchAsync(
        string query,
        int maxResults,
        CancellationToken cancellationToken)
    {
        LoggingService.LogInformation($"Everything search requested. Query='{query}', maxResults={maxResults}.");

        await EverythingSdkBootstrapper.EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(query))
        {
            LoggingService.LogInformation("Everything search skipped because the query was empty.");
            return Array.Empty<EverythingSearchResult>();
        }

        maxResults = Math.Clamp(maxResults, 1, 2000);
        LoggingService.LogInformation($"Everything search normalized parameters. maxResults={maxResults}.");

        var stopwatch = Stopwatch.StartNew();

        var entered = false;
        try
        {
            await _queryGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            entered = true;

            var execution = await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return ExecuteSearchInternal(query, maxResults, cancellationToken);
            }, cancellationToken).ConfigureAwait(false);

            Volatile.Write(ref _lastExcludedResultCount, execution.FilteredCount);
            stopwatch.Stop();
            LoggingService.LogInformation(
                $"Everything search completed in {stopwatch.Elapsed.TotalMilliseconds:F0} ms. " +
                $"Results={execution.Results.Count}, excluded={execution.FilteredCount}.");
            return execution.Results;
        }
        catch (DllNotFoundException ex)
        {
            stopwatch.Stop();
            LoggingService.LogError("Everything search failed: SDK DLL not found.", ex);
            throw EverythingSearchException.MissingLibrary(ex);
        }
        catch (EntryPointNotFoundException ex)
        {
            stopwatch.Stop();
            LoggingService.LogError("Everything search failed: SDK DLL entry point mismatch.", ex);
            throw EverythingSearchException.MismatchedLibrary(ex);
        }
        finally
        {
            stopwatch.Stop();
            if (entered)
                _queryGate.Release();
        }
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
    {
        LoggingService.LogInformation("Checking Everything search availability.");
        await EverythingSdkBootstrapper.EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var entered = false;
        try
        {
            await _queryGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            entered = true;

            var available = await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return EverythingNative.IsDbLoaded();
            }, cancellationToken).ConfigureAwait(false);

            Volatile.Write(ref _availabilityHint, available
                ? "Using Everything SDK for instant .unitypackage search."
                : "Everything is not running. Launch the Everything desktop app to enable .unitypackage search.");

            LoggingService.LogInformation(
                $"Everything availability check result: {(available ? "available" : "unavailable")}.");
            return available;
        }
        catch (DllNotFoundException ex)
        {
            Volatile.Write(ref _availabilityHint,
                "Everything SDK DLL is missing. Download the official Everything SDK or allow EasyExtract to fetch it automatically.");
            LoggingService.LogError("Everything availability check failed: SDK DLL missing.", ex);
            throw EverythingSearchException.MissingLibrary(ex);
        }
        catch (EntryPointNotFoundException ex)
        {
            Volatile.Write(ref _availabilityHint,
                "The Everything SDK DLL does not match this architecture. Replace it with the correct 32/64-bit build.");
            LoggingService.LogError("Everything availability check failed: SDK DLL entry point mismatch.", ex);
            throw EverythingSearchException.MismatchedLibrary(ex);
        }
        finally
        {
            if (entered)
                _queryGate.Release();
        }
    }
}