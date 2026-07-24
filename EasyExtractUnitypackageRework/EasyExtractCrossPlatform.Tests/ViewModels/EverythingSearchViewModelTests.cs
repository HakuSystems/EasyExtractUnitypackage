using EasyExtractCrossPlatform.Models;
using EasyExtractCrossPlatform.Services;
using EasyExtractCrossPlatform.Tests.Support;
using EasyExtractCrossPlatform.ViewModels;
using Xunit;

namespace EasyExtractCrossPlatform.Tests.ViewModels;

public sealed class EverythingSearchViewModelTests
{
    /// <summary>
    ///     The auto search timer fires while the user keeps typing, so most searches are superseded
    ///     before they finish. Those runs used to keep their CancellationTokenSource forever because
    ///     only the currently active one was disposed.
    /// </summary>
    [Fact]
    public Task SupersededSearch_DisposesItsCancellationTokenSource()
    {
        return HeadlessUi.RunAsync(async () =>
        {
            var service = new GatedSearchService();
            using var viewModel = new EverythingSearchViewModel(service);

            viewModel.SearchQuery = "first";
            var firstRun = viewModel.SearchCommand.ExecuteAsync(null);
            var firstToken = await service.WaitForNextTokenAsync().ConfigureAwait(true);

            viewModel.SearchQuery = "second";
            var secondRun = viewModel.SearchCommand.ExecuteAsync(null);
            await service.WaitForNextTokenAsync().ConfigureAwait(true);

            service.ReleaseAll();
            await firstRun.ConfigureAwait(true);
            await secondRun.ConfigureAwait(true);

            Assert.Throws<ObjectDisposedException>(() => firstToken.WaitHandle);
        });
    }

    [Fact]
    public Task CompletedSearch_DisposesItsCancellationTokenSource()
    {
        return HeadlessUi.RunAsync(async () =>
        {
            var service = new GatedSearchService();
            using var viewModel = new EverythingSearchViewModel(service);

            viewModel.SearchQuery = "only";
            var run = viewModel.SearchCommand.ExecuteAsync(null);
            var token = await service.WaitForNextTokenAsync().ConfigureAwait(true);

            service.ReleaseAll();
            await run.ConfigureAwait(true);

            Assert.Throws<ObjectDisposedException>(() => token.WaitHandle);
        });
    }

    private sealed class GatedSearchService : IEverythingSearchService
    {
        private readonly List<TaskCompletionSource> _gates = [];
        private readonly Queue<TaskCompletionSource<CancellationToken>> _pendingTokenWaiters = new();
        private readonly Queue<CancellationToken> _observedTokens = new();

        public int LastExcludedResultCount => 0;

        public string AvailabilityHint => "test";

        public Task<IReadOnlyList<EverythingSearchResult>> SearchAsync(string query, int maxResults,
            CancellationToken cancellationToken)
        {
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _gates.Add(gate);
            PublishToken(cancellationToken);

            return gate.Task.ContinueWith(
                _ => (IReadOnlyList<EverythingSearchResult>)Array.Empty<EverythingSearchResult>(),
                TaskScheduler.Default);
        }

        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        public Task<CancellationToken> WaitForNextTokenAsync()
        {
            if (_observedTokens.Count > 0)
                return Task.FromResult(_observedTokens.Dequeue());

            var waiter = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingTokenWaiters.Enqueue(waiter);
            return waiter.Task;
        }

        public void ReleaseAll()
        {
            foreach (var gate in _gates)
                gate.TrySetResult();
        }

        private void PublishToken(CancellationToken token)
        {
            if (_pendingTokenWaiters.Count > 0)
                _pendingTokenWaiters.Dequeue().TrySetResult(token);
            else
                _observedTokens.Enqueue(token);
        }
    }
}
