using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using EasyExtractCrossPlatform.Models;
using EasyExtractCrossPlatform.Services;
using Xunit;

namespace EasyExtractCrossPlatform.Tests.Services;

public sealed class HakuSyncServiceTests
{
    [Fact]
    public async Task SyncActivityAsync_DedupesConcurrentCalls_ForSameEntry()
    {
        var deviceId = Guid.NewGuid().ToString("D");
        using var handler = new RecordingHttpMessageHandler(async (request, _) =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/auth/anonymous-session",
                    StringComparison.OrdinalIgnoreCase))
                return CreateAnonymousSessionResponse(deviceId, "sync-token");

            await Task.Delay(75);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var httpClient = CreateHttpClient(handler);
        var service = new HakuSyncService(httpClient);
        var entry = CreateEntry(Guid.NewGuid());

        var tasks = Enumerable.Range(0, 10)
            .Select(_ => service.SyncActivityAsync(deviceId, entry))
            .ToArray();

        await Task.WhenAll(tasks);

        Assert.Equal(1, handler.CountRequests("/auth/anonymous-session"));
        Assert.Equal(1, handler.CountRequests("/dashboard/activity"));
        Assert.Equal("Bearer sync-token", handler.GetSingleRequest("/dashboard/activity").Authorization);
    }

    [Fact]
    public async Task SyncActivityAsync_DoesNotDedup_ForDifferentEntryIds()
    {
        var deviceId = Guid.NewGuid().ToString("D");
        using var handler = new RecordingHttpMessageHandler(async (request, _) =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/auth/anonymous-session",
                    StringComparison.OrdinalIgnoreCase))
                return CreateAnonymousSessionResponse(deviceId, "sync-token");

            await Task.Delay(75);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var httpClient = CreateHttpClient(handler);
        var service = new HakuSyncService(httpClient);

        var first = CreateEntry(Guid.NewGuid());
        var second = CreateEntry(Guid.NewGuid());
        var tasks = new[]
        {
            service.SyncActivityAsync(deviceId, first),
            service.SyncActivityAsync(deviceId, first),
            service.SyncActivityAsync(deviceId, second),
            service.SyncActivityAsync(deviceId, second)
        };

        await Task.WhenAll(tasks);

        Assert.Equal(1, handler.CountRequests("/auth/anonymous-session"));
        Assert.Equal(2, handler.CountRequests("/dashboard/activity"));
    }

    [Fact]
    public async Task SyncActivityAsync_DoesNotSend_WhenDeviceIdMissing()
    {
        using var handler = new RecordingHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        using var httpClient = CreateHttpClient(handler);
        var service = new HakuSyncService(httpClient);
        var entry = CreateEntry(Guid.NewGuid());

        await service.SyncActivityAsync(string.Empty, entry);
        await service.SyncActivityAsync("   ", entry);

        Assert.Equal(0, handler.TotalRequestCount);
    }

    [Fact]
    public async Task SyncActivityAsync_DoesNotSend_WhenDeviceIdIsInvalid()
    {
        using var handler = new RecordingHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        using var httpClient = CreateHttpClient(handler);
        var service = new HakuSyncService(httpClient);
        var entry = CreateEntry(Guid.NewGuid());

        await service.SyncActivityAsync("not-a-guid", entry);

        Assert.Equal(0, handler.TotalRequestCount);
    }

    [Fact]
    public async Task SyncActivityAsync_CleansInFlightEntry_AfterFailure()
    {
        var deviceId = Guid.NewGuid().ToString("D");
        using var handler = new RecordingHttpMessageHandler((request, _) =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/auth/anonymous-session",
                    StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(CreateAnonymousSessionResponse(deviceId, "sync-token"));

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        });
        using var httpClient = CreateHttpClient(handler);
        var service = new HakuSyncService(httpClient);
        var entry = CreateEntry(Guid.NewGuid());

        await service.SyncActivityAsync(deviceId, entry);
        await service.SyncActivityAsync(deviceId, entry);

        Assert.Equal(1, handler.CountRequests("/auth/anonymous-session"));
        Assert.Equal(2, handler.CountRequests("/dashboard/activity"));
    }

    [Fact]
    public async Task SyncActivityAsync_CleansInFlightEntry_AfterCallerCancellation()
    {
        var deviceId = Guid.NewGuid().ToString("D");
        var completionSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var requestCompletedSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new RecordingHttpMessageHandler(async (request, _) =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/auth/anonymous-session",
                    StringComparison.OrdinalIgnoreCase))
                return CreateAnonymousSessionResponse(deviceId, "sync-token");

            await completionSignal.Task;
            requestCompletedSignal.TrySetResult();
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var httpClient = CreateHttpClient(handler);
        var service = new HakuSyncService(httpClient);
        var entry = CreateEntry(Guid.NewGuid());

        using (var cts = new CancellationTokenSource())
        {
            var firstCall = service.SyncActivityAsync(deviceId, entry, cts.Token);
            cts.Cancel();
            await firstCall;
        }

        completionSignal.SetResult();
        await requestCompletedSignal.Task;
        await WaitForRequestCountAsync(handler, "/dashboard/activity", 1, TimeSpan.FromSeconds(2));
        await Task.Delay(25);

        await service.SyncActivityAsync(deviceId, entry);

        Assert.Equal(1, handler.CountRequests("/auth/anonymous-session"));
        Assert.Equal(2, handler.CountRequests("/dashboard/activity"));
    }

    private static HttpClient CreateHttpClient(HttpMessageHandler handler)
    {
        return new HttpClient(handler)
        {
            BaseAddress = new Uri("https://easyextract.net/api/haku/v1/")
        };
    }

    private static HistoryEntry CreateEntry(Guid id)
    {
        return new HistoryEntry
        {
            Id = id,
            FileName = "package.unitypackage",
            FileSizeBytes = 1024,
            WasExtracted = true,
            ExtractedFilesCount = 4,
            AssetsExtracted = 12,
            ExtractionDurationMs = 2500,
            AddedUtc = DateTimeOffset.UtcNow
        };
    }

    private static HttpResponseMessage CreateAnonymousSessionResponse(string deviceId, string token)
    {
        return CreateJsonResponse(new
        {
            status = "success",
            data = new
            {
                token,
                deviceId,
                expiresAtUtc = DateTimeOffset.UtcNow.AddHours(1),
                scopes = new[] { "dashboard:write" }
            }
        });
    }

    private static HttpResponseMessage CreateJsonResponse(object payload)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
    }

    private static async Task WaitForRequestCountAsync(
        RecordingHttpMessageHandler handler,
        string pathSuffix,
        int expectedCount,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (handler.CountRequests(pathSuffix) >= expectedCount)
                return;

            await Task.Delay(10);
        }

        Assert.True(handler.CountRequests(pathSuffix) >= expectedCount, $"Expected at least {expectedCount} requests.");
    }

    private sealed class RecordingHttpMessageHandler : HttpMessageHandler, IDisposable
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;
        private readonly ConcurrentQueue<RequestRecord> _requests = new();
        private readonly ConcurrentBag<HttpResponseMessage> _responses = new();

        public RecordingHttpMessageHandler(
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        public int TotalRequestCount => _requests.Count;

        public new void Dispose()
        {
            while (_responses.TryTake(out var response))
                response.Dispose();

            base.Dispose();
        }

        public int CountRequests(string pathSuffix)
        {
            return _requests.Count(request =>
                request.Path.EndsWith(pathSuffix, StringComparison.OrdinalIgnoreCase));
        }

        public RequestRecord GetSingleRequest(string pathSuffix)
        {
            return _requests.Single(request =>
                request.Path.EndsWith(pathSuffix, StringComparison.OrdinalIgnoreCase));
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            _requests.Enqueue(new RequestRecord(
                request.RequestUri?.AbsolutePath ?? string.Empty,
                request.Headers.Authorization?.ToString()));

            var response = await _handler(request, cancellationToken);
            _responses.Add(response);
            return response;
        }
    }

    private sealed record RequestRecord(string Path, string? Authorization);
}