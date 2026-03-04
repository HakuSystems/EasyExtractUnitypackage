using System.Collections.Concurrent;
using System.Net;
using EasyExtractCrossPlatform.Models;
using EasyExtractCrossPlatform.Services;
using Xunit;

namespace EasyExtractCrossPlatform.Tests.Services;

public sealed class HakuSyncServiceTests
{
    [Fact]
    public async Task SyncActivityAsync_DedupesConcurrentCalls_ForSameEntry()
    {
        using var handler = new CountingHttpMessageHandler(async (_, _) =>
        {
            await Task.Delay(75);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var httpClient = CreateHttpClient(handler);
        var service = new HakuSyncService(httpClient);
        var entry = CreateEntry(Guid.NewGuid());
        var deviceId = Guid.NewGuid().ToString();

        var tasks = Enumerable.Range(0, 10)
            .Select(_ => service.SyncActivityAsync(deviceId, entry))
            .ToArray();

        await Task.WhenAll(tasks);

        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task SyncActivityAsync_DoesNotDedup_ForDifferentEntryIds()
    {
        using var handler = new CountingHttpMessageHandler(async (_, _) =>
        {
            await Task.Delay(75);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var httpClient = CreateHttpClient(handler);
        var service = new HakuSyncService(httpClient);
        var deviceId = Guid.NewGuid().ToString();

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

        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task SyncActivityAsync_DoesNotSend_WhenDeviceIdMissing()
    {
        using var handler = new CountingHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        using var httpClient = CreateHttpClient(handler);
        var service = new HakuSyncService(httpClient);
        var entry = CreateEntry(Guid.NewGuid());

        await service.SyncActivityAsync(string.Empty, entry);
        await service.SyncActivityAsync("   ", entry);

        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task SyncActivityAsync_CleansInFlightEntry_AfterFailure()
    {
        using var handler = new CountingHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));
        using var httpClient = CreateHttpClient(handler);
        var service = new HakuSyncService(httpClient);
        var entry = CreateEntry(Guid.NewGuid());
        var deviceId = Guid.NewGuid().ToString();

        await service.SyncActivityAsync(deviceId, entry);
        await service.SyncActivityAsync(deviceId, entry);

        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task SyncActivityAsync_CleansInFlightEntry_AfterCallerCancellation()
    {
        var completionSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var requestCompletedSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new CountingHttpMessageHandler(async (_, _) =>
        {
            await completionSignal.Task;
            requestCompletedSignal.SetResult();
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var httpClient = CreateHttpClient(handler);
        var service = new HakuSyncService(httpClient);
        var entry = CreateEntry(Guid.NewGuid());
        var deviceId = Guid.NewGuid().ToString();

        using (var cts = new CancellationTokenSource())
        {
            var firstCall = service.SyncActivityAsync(deviceId, entry, cts.Token);
            cts.Cancel();
            await firstCall;
        }

        completionSignal.SetResult();
        await requestCompletedSignal.Task;
        await WaitForCallCountAsync(handler, 1, TimeSpan.FromSeconds(2));
        await Task.Delay(25);

        await service.SyncActivityAsync(deviceId, entry);

        Assert.Equal(2, handler.CallCount);
    }

    private static HttpClient CreateHttpClient(HttpMessageHandler handler)
    {
        return new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.hakusystems.dev/api/v1/")
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

    private static async Task WaitForCallCountAsync(
        CountingHttpMessageHandler handler,
        int expectedCount,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (handler.CallCount >= expectedCount)
                return;

            await Task.Delay(10);
        }

        Assert.True(handler.CallCount >= expectedCount, $"Expected at least {expectedCount} calls.");
    }

    private sealed class CountingHttpMessageHandler : HttpMessageHandler, IDisposable
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;
        private readonly ConcurrentBag<HttpResponseMessage> _responses = new();
        private int _callCount;

        public CountingHttpMessageHandler(
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        public int CallCount => Volatile.Read(ref _callCount);

        public new void Dispose()
        {
            while (_responses.TryTake(out var response))
                response.Dispose();

            base.Dispose();
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _callCount);
            var response = await _handler(request, cancellationToken);
            _responses.Add(response);
            return response;
        }
    }
}