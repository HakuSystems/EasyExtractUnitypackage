using System.Net;
using EasyExtractCrossPlatform.Services;
using Xunit;

namespace EasyExtractCrossPlatform.Tests.Services;

public sealed class HakuWebhookClientTests
{
    [Fact]
    public async Task SendFeedbackAsync_UsesWebsiteProxyEndpoint_WhenNoBaseAddressIsConfigured()
    {
        Uri? requestedUri = null;
        using var handler = new CapturingHttpMessageHandler((request, _) =>
        {
            requestedUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });
        using var httpClient = new HttpClient(handler);
        var client = new HakuWebhookClient(httpClient, "EasyExtract");

        await client.SendFeedbackAsync("Test feedback", "1.0.0");

        Assert.Equal(new Uri("https://easyextract.net/api/haku/webhooks/send"), requestedUri);
    }

    private sealed class CapturingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public CapturingHttpMessageHandler(
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return _handler(request, cancellationToken);
        }
    }
}