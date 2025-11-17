using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
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
        {
            LoggingService.LogWarning("PostErrorAsync: empty payload | action=skipped");
            return;
        }

        var willTruncate = logPayload.Length > MaxLogExcerptLength;
        LoggingService.LogInformation(
            $"PostErrorAsync: scheduling auto webhook | payloadChars={logPayload.Length} | willTruncate={willTruncate}");

        var payload = BuildErrorPayload(logPayload);
        _ = Task.Run(() => SendPayloadAsyncInternal(ErrorWebhookUrl, payload, CancellationToken.None, true));
    }

    public static Task SendPayloadAsync(string webhookUrl, object payload,
        CancellationToken cancellationToken = default)
    {
        var webhookId = GetWebhookIdentity(webhookUrl);
        var payloadType = payload?.GetType().Name ?? "null";
        LoggingService.LogInformation(
            $"SendPayloadAsync: dispatch requested | webhookId={webhookId} | payloadType={payloadType}");
        return SendPayloadAsyncInternal(webhookUrl, payload, cancellationToken, false);
    }

    private static async Task SendPayloadAsyncInternal(string webhookUrl, object payload,
        CancellationToken cancellationToken, bool swallowExceptions)
    {
        var webhookId = GetWebhookIdentity(webhookUrl);
        var payloadType = payload?.GetType().Name ?? "null";
        LoggingService.LogInformation(
            $"SendPayloadAsyncInternal: begin send | webhookId={webhookId} | swallowExceptions={swallowExceptions} | payloadType={payloadType}");

        try
        {
            await DeliverPayloadAsync(webhookUrl, payload, cancellationToken).ConfigureAwait(false);
            LoggingService.LogInformation($"SendPayloadAsyncInternal: send completed | webhookId={webhookId}");
        }
        catch (Exception ex) when (swallowExceptions)
        {
            LoggingService.LogWarning(
                $"SendPayloadAsyncInternal: delivery failed (suppressed) | webhookId={webhookId}", ex);
        }
        catch (Exception ex)
        {
            LoggingService.LogError($"SendPayloadAsyncInternal: delivery failed | webhookId={webhookId}", ex);
            throw;
        }
    }

    private static async Task DeliverPayloadAsync(string webhookUrl, object payload,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(webhookUrl))
        {
            LoggingService.LogError("DeliverPayloadAsync: webhook URL missing | action=throw");
            throw new ArgumentException("Webhook URL must not be empty.", nameof(webhookUrl));
        }

        if (payload is null)
        {
            LoggingService.LogError("DeliverPayloadAsync: payload is null | action=throw");
            throw new ArgumentNullException(nameof(payload));
        }

        var webhookId = GetWebhookIdentity(webhookUrl);
        var payloadType = payload.GetType().Name;
        var json = JsonSerializer.Serialize(payload);
        var jsonBytes = Encoding.UTF8.GetByteCount(json);
        LoggingService.LogInformation(
            $"DeliverPayloadAsync: payload serialized | webhookId={webhookId} | payloadType={payloadType} | jsonBytes={jsonBytes}");

        await EnforceRateLimitAsync(webhookUrl, cancellationToken).ConfigureAwait(false);

        using (LoggingService.BeginPerformanceScope("DiscordWebhookSend", "Networking",
                   webhookId))
        {
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response =
                await HttpClient.PostAsync(webhookUrl, content, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                var preview = TruncateForLog(body);
                LoggingService.LogError(
                    $"DeliverPayloadAsync: Discord rejected payload | webhookId={webhookId} | status={(int)response.StatusCode} | responsePreview='{preview}'");
                throw new HttpRequestException(
                    $"Discord webhook responded with status {(int)response.StatusCode}: {preview}");
            }

            LoggingService.LogInformation(
                $"DeliverPayloadAsync: Discord accepted payload | webhookId={webhookId} | status={(int)response.StatusCode}");
        }
    }

    private static async Task EnforceRateLimitAsync(string webhookUrl, CancellationToken cancellationToken)
    {
        var webhookId = GetWebhookIdentity(webhookUrl);
        await SendGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            LastSentByWebhook.TryGetValue(webhookUrl, out var lastSent);
            var now = DateTimeOffset.UtcNow;
            var waitDuration = lastSent + MinimumSendInterval - now;
            if (waitDuration > TimeSpan.Zero)
            {
                LoggingService.LogInformation(
                    $"EnforceRateLimitAsync: throttling webhook | webhookId={webhookId} | waitMs={waitDuration.TotalMilliseconds:F0}");
                await Task.Delay(waitDuration, cancellationToken).ConfigureAwait(false);
            }

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
        var sanitizedLength = sanitized.Length;
        var truncated = sanitizedLength > MaxLogExcerptLength;
        if (truncated)
        {
            LoggingService.LogInformation(
                $"BuildErrorPayload: truncating log excerpt | originalChars={sanitizedLength} | maxChars={MaxLogExcerptLength}");
            sanitized = sanitized[..MaxLogExcerptLength] + Environment.NewLine + "... (truncated)";
        }
        else
        {
            LoggingService.LogInformation(
                $"BuildErrorPayload: log excerpt ready | chars={sanitizedLength}");
        }

        var builder = new StringBuilder()
            .AppendLine("[!] **EasyExtract error report**")
            .AppendLine($"**Host:** {Environment.MachineName}")
            .AppendLine($"**Time (UTC):** {DateTimeOffset.UtcNow:O}")
            .AppendLine("```")
            .AppendLine(sanitized)
            .AppendLine("```");

        return new DiscordWebhookPayload(builder.ToString());
    }

    private static string GetWebhookIdentity(string? webhookUrl)
    {
        if (string.IsNullOrWhiteSpace(webhookUrl))
            return "<empty>";

        try
        {
            var uri = new Uri(webhookUrl, UriKind.Absolute);
            var fingerprintSource = string.IsNullOrWhiteSpace(uri.AbsolutePath)
                ? uri.ToString()
                : uri.AbsolutePath;
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(fingerprintSource));
            return $"{uri.Host}:{Convert.ToHexString(hash.AsSpan(0, 4))}";
        }
        catch
        {
            var hashCode = unchecked((uint)webhookUrl.GetHashCode());
            return $"unknown:{hashCode:X8}";
        }
    }

    private static string TruncateForLog(string? value, int maxLength = 200)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var normalized = value.ReplaceLineEndings(" ");
        return normalized.Length <= maxLength
            ? normalized
            : normalized[..maxLength] + "...";
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