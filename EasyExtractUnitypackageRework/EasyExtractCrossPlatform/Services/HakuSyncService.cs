using System.Net.Http.Json;

namespace EasyExtractCrossPlatform.Services;

public interface IHakuSyncService
{
    Task SyncActivityAsync(string deviceId, HistoryEntry entry, CancellationToken cancellationToken = default);
}

public class HakuSyncService : IHakuSyncService
{
    private const string ApiBaseUrl = "https://api.hakusystems.dev/api/v1/";
    private readonly HttpClient _httpClient;

    public HakuSyncService()
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(ApiBaseUrl),
            Timeout = TimeSpan.FromSeconds(10)
        };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("EasyExtractCrossPlatform-SyncService");
    }

    public async Task SyncActivityAsync(string deviceId, HistoryEntry entry,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
            return;

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

            var request = new HttpRequestMessage(HttpMethod.Post, "dashboard/activity");
            request.Headers.Add("X-Device-Id", deviceId);
            request.Content = JsonContent.Create(payload);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception)
        {
            // Silently fail
        }
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