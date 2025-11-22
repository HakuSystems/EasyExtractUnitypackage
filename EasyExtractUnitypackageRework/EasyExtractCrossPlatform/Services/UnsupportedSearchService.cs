namespace EasyExtractCrossPlatform.Services;

internal sealed class UnsupportedSearchService : IEverythingSearchService
{
    public UnsupportedSearchService(string platformName)
    {
        AvailabilityHint = $"Search is not supported on {platformName}.";
    }

    public int LastExcludedResultCount => 0;

    public string AvailabilityHint { get; }

    public Task<IReadOnlyList<EverythingSearchResult>> SearchAsync(
        string query,
        int maxResults,
        CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<EverythingSearchResult>>(Array.Empty<EverythingSearchResult>());
    }

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(false);
    }
}