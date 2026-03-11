using System.Net;
using EasyExtractCrossPlatform.Services;
using Velopack;
using Xunit;

namespace EasyExtractCrossPlatform.Tests.Services;

public sealed class VelopackUpdateServiceTests
{
    [Fact]
    public async Task CheckForUpdatesAsync_SilentBackgroundRateLimit_ReturnsUnavailable()
    {
        var service = new VelopackUpdateService(_ => new FakeUpdateManager
        {
            IsInstalled = true,
            CheckForUpdatesAsyncImpl = () =>
                throw new HttpRequestException("API rate limit exceeded", null, HttpStatusCode.Forbidden)
        });

        var result = await service.CheckForUpdatesAsync(UpdateCheckMode.SilentBackground);

        Assert.Equal(UpdateCheckState.Unavailable, result.State);
        Assert.Null(result.UpdateInfo);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_InteractiveRateLimit_Throws()
    {
        var service = new VelopackUpdateService(_ => new FakeUpdateManager
        {
            IsInstalled = true,
            CheckForUpdatesAsyncImpl = () =>
                throw new HttpRequestException("API rate limit exceeded", null, HttpStatusCode.Forbidden)
        });

        await Assert.ThrowsAsync<HttpRequestException>(() => service.CheckForUpdatesAsync());
    }

    private sealed class FakeUpdateManager : IVelopackUpdateManager
    {
        public Func<Task<UpdateInfo?>>? CheckForUpdatesAsyncImpl { get; init; }
        public Func<UpdateInfo, Action<int>, Task>? DownloadUpdatesAsyncImpl { get; init; }
        public Action<UpdateInfo>? ApplyUpdatesAndRestartImpl { get; init; }
        public bool IsInstalled { get; init; }

        public Task<UpdateInfo?> CheckForUpdatesAsync()
        {
            return CheckForUpdatesAsyncImpl?.Invoke() ?? Task.FromResult<UpdateInfo?>(null);
        }

        public Task DownloadUpdatesAsync(UpdateInfo update, Action<int> progress)
        {
            return DownloadUpdatesAsyncImpl?.Invoke(update, progress) ?? Task.CompletedTask;
        }

        public void ApplyUpdatesAndRestart(UpdateInfo update)
        {
            ApplyUpdatesAndRestartImpl?.Invoke(update);
        }
    }
}