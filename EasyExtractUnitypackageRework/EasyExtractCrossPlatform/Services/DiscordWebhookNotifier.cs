using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace EasyExtractCrossPlatform.Services;

internal static class DiscordWebhookNotifier
{
    private const string ErrorWebhookUrl =
        "https://discord.com/api/webhooks/1439780844863619186/LzKyW_oFIQD-yF_14MhyIJ7Kv0Ip9XLwnBQA59uhnhBEjMDWZLgZJmV9K1neZmQHVSO0";

    private const string UserAgent = "EasyExtractCrossPlatform-WebhookClient";

    private const int MaxLogExcerptLength = 1800;
    private static readonly TimeSpan MinimumSendInterval = TimeSpan.FromSeconds(5);
    private static readonly HttpClient HttpClient = CreateHttpClient();
    private static readonly SemaphoreSlim SendGate = new(1, 1);

    private static readonly Dictionary<string, DateTimeOffset> LastSentByWebhook =
        new(StringComparer.OrdinalIgnoreCase);

    public static void PostErrorAsync(string logPayload)
    {
        if (string.IsNullOrWhiteSpace(logPayload))
            return;

        var payload = BuildErrorPayload(logPayload);
        _ = Task.Run(() => SendPayloadAsyncInternal(ErrorWebhookUrl, payload, CancellationToken.None, true));
    }

    public static Task SendPayloadAsync(string webhookUrl, object payload,
        CancellationToken cancellationToken = default)
    {
        return SendPayloadAsyncInternal(webhookUrl, payload, cancellationToken, false);
    }

    private static async Task SendPayloadAsyncInternal(string webhookUrl, object payload,
        CancellationToken cancellationToken, bool swallowExceptions)
    {
        try
        {
            await DeliverPayloadAsync(webhookUrl, payload, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (swallowExceptions)
        {
            LoggingService.LogWarning("Failed to deliver Discord webhook notification.", ex);
        }
    }

    private static async Task DeliverPayloadAsync(string webhookUrl, object payload,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(webhookUrl))
            throw new ArgumentException("Webhook URL must not be empty.", nameof(webhookUrl));
        if (payload is null)
            throw new ArgumentNullException(nameof(payload));

        var json = JsonSerializer.Serialize(payload);

        await EnforceRateLimitAsync(webhookUrl, cancellationToken).ConfigureAwait(false);

        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await HttpClient.PostAsync(webhookUrl, content, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new HttpRequestException(
                $"Discord webhook responded with status {(int)response.StatusCode}: {body}");
        }
    }

    private static async Task EnforceRateLimitAsync(string webhookUrl, CancellationToken cancellationToken)
    {
        await SendGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            LastSentByWebhook.TryGetValue(webhookUrl, out var lastSent);
            var now = DateTimeOffset.UtcNow;
            var waitDuration = lastSent + MinimumSendInterval - now;
            if (waitDuration > TimeSpan.Zero)
                await Task.Delay(waitDuration, cancellationToken).ConfigureAwait(false);

            LastSentByWebhook[webhookUrl] = DateTimeOffset.UtcNow;
        }
        finally
        {
            SendGate.Release();
        }
    }

    private static DiscordWebhookPayload BuildErrorPayload(string logPayload)
    {
        var sanitized = logPayload.Replace("```", "`\u200B``", StringComparison.Ordinal);
        if (sanitized.Length > MaxLogExcerptLength)
            sanitized = sanitized[..MaxLogExcerptLength] + Environment.NewLine + "... (truncated)";

        var builder = new StringBuilder()
            .AppendLine("[!] **EasyExtract error report**")
            .AppendLine($"**Host:** {Environment.MachineName}")
            .AppendLine($"**Time (UTC):** {DateTimeOffset.UtcNow:O}")
            .AppendLine("```")
            .AppendLine(sanitized)
            .AppendLine("```");

        return new DiscordWebhookPayload(builder.ToString());
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        try
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        }
        catch
        {
            // If setting the header fails we can still send webhooks.
        }

        return client;
    }

    private sealed record DiscordWebhookPayload(string Content);
}