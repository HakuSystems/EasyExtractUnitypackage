using System.Collections.Concurrent;
using System.Net.Http.Json;

namespace EasyExtractCrossPlatform.Services;

public interface IHakuSyncService
{
    Task SyncActivityAsync(string deviceId, HistoryEntry entry, CancellationToken cancellationToken = default);
}

public class HakuSyncService : IHakuSyncService
{
    private const string ApiBaseUrl = "https://api.hakusystems.dev/api/v1/";
    private const string UserAgent = "EasyExtractCrossPlatform-SyncService";
    private readonly HttpClient _httpClient;
    private readonly ConcurrentDictionary<string, Lazy<Task>> _inFlightSyncs = new(StringComparer.Ordinal);

    public HakuSyncService() : this(CreateHttpClient())
    {
    }

    internal HakuSyncService(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

        if (_httpClient.BaseAddress is null)
            _httpClient.BaseAddress = new Uri(ApiBaseUrl);

        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
    }

    public async Task SyncActivityAsync(string deviceId, HistoryEntry entry,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
            return;

        var normalizedDeviceId = deviceId.Trim();
        var dedupeKey = $"{normalizedDeviceId}:{entry.Id:D}";
        var lazyTask = new Lazy<Task>(
            () => SendSyncRequestAsync(normalizedDeviceId, entry),
            LazyThreadSafetyMode.ExecutionAndPublication);
        var inFlightEntry = _inFlightSyncs.GetOrAdd(dedupeKey, lazyTask);
        var inFlightTask = inFlightEntry.Value;

        if (ReferenceEquals(inFlightEntry, lazyTask))
            _ = inFlightTask.ContinueWith(
                _ => _inFlightSyncs.TryRemove(new KeyValuePair<string, Lazy<Task>>(dedupeKey, inFlightEntry)),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);

        try
        {
            await inFlightTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The caller canceled while a shared request may still be in-flight for other callers.
        }
    }

    private async Task SendSyncRequestAsync(string deviceId, HistoryEntry entry)
    {
        try
        {
            var payload = new SyncActivityRequest
            {
                Id = entry.Id,
                FileName = entry.FileName,
                FileSize = entry.FileSizeBytes,
                Status = entry.WasExtracted || entry.ExtractedFilesCount > 0 || entry.AssetsExtracted > 0
                    ? "Completed"
                    : "Failed",
                ExtractedFileCount = entry.ExtractedFilesCount,
                DurationMs = entry.ExtractionDurationMs,
                CreatedAt = entry.AddedUtc
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, "dashboard/activity");
            request.Headers.Add("X-Device-Id", deviceId);
            request.Content = JsonContent.Create(payload);

            using var response = await _httpClient.SendAsync(request, CancellationToken.None).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception)
        {
            // Silently fail
        }
    }

    private static HttpClient CreateHttpClient()
    {
        var httpClient = new HttpClient
        {
            BaseAddress = new Uri(ApiBaseUrl),
            Timeout = TimeSpan.FromSeconds(10)
        };
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        return httpClient;
    }
}

public class SyncActivityRequest
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string Status { get; set; } = "UNKNOWN";
    public int ExtractedFileCount { get; set; }
    public double DurationMs { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}