using System.Net;
using System.Text;
using System.Text.Json;
using EasyExtractCrossPlatform.Services;
using Xunit;

namespace EasyExtractCrossPlatform.Tests.Services;

public sealed class HakuWebhookClientTests
{
    [Fact]
    public async Task SendFeedbackAsync_UsesWebsiteProxyEndpoint_WithAnonymousSessionAuthorization()
    {
        var deviceId = Guid.NewGuid().ToString("D");
        RequestRecord? webhookRequest = null;

        using var handler = new CapturingHttpMessageHandler((request, _) =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/auth/anonymous-session",
                    StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(CreateJsonResponse(new
                {
                    status = "success",
                    data = new
                    {
                        token = "webhook-token",
                        deviceId,
                        expiresAtUtc = DateTimeOffset.UtcNow.AddHours(1),
                        scopes = new[]
                        {
                            "webhook:send"
                        }
                    }
                }));

            webhookRequest = new RequestRecord(
                request.RequestUri,
                request.Headers.Authorization?.ToString());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        using var httpClient = new HttpClient(handler);
        var client = new HakuWebhookClient(httpClient, "EasyExtract", () => deviceId);

        await client.SendFeedbackAsync("Test feedback", "1.0.0");

        Assert.NotNull(webhookRequest);
        Assert.Equal(new Uri("https://easyextract.net/api/haku/webhooks/send"), webhookRequest!.Uri);
        Assert.Equal("Bearer webhook-token", webhookRequest.Authorization);
    }

    private static HttpResponseMessage CreateJsonResponse(object payload)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
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

    private sealed record RequestRecord(Uri? Uri, string? Authorization);
}