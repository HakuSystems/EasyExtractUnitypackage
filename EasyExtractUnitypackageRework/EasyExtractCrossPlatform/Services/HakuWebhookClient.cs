using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Serialization;

namespace EasyExtractCrossPlatform.Services;

public sealed class WebhookRequest
{
    [JsonPropertyName("application")] public string Application { get; set; } = string.Empty;

    [JsonPropertyName("message")] public string Message { get; set; } = string.Empty;

    [JsonPropertyName("version")] public string? Version { get; set; }

    [JsonPropertyName("os")] public string? Os { get; set; }

    [JsonPropertyName("isError")] public bool IsError { get; set; }

    [JsonPropertyName("webhookType")] public string? WebhookType { get; set; }
}

public sealed class HakuWebhookClient
{
    internal const string ApiBaseUrl = "https://api.hakusystems.dev";
    private const string EndpointPath = "api/webhooks/send";
    private readonly string _appName;

    private readonly HttpClient _httpClient;

    public HakuWebhookClient(HttpClient httpClient, string appName)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _appName = string.IsNullOrWhiteSpace(appName)
            ? throw new ArgumentException("Application name must not be empty.", nameof(appName))
            : appName;

        if (_httpClient.BaseAddress is null || !_httpClient.BaseAddress.IsAbsoluteUri)
            _httpClient.BaseAddress = new Uri(ApiBaseUrl);
    }

    public Task SendFeedbackAsync(string feedbackText, string? currentVersion,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(feedbackText))
            throw new ArgumentException("Feedback text must not be empty.", nameof(feedbackText));

        var payload = CreateBasePayload(feedbackText, currentVersion, false);
        payload.WebhookType = "Feedback";

        return SendAsync(payload, cancellationToken);
    }

    public Task SendErrorReportAsync(Exception exception, string? currentVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return SendErrorReportAsync(exception.ToString(), currentVersion, cancellationToken: cancellationToken);
    }

    public Task SendErrorReportAsync(string message, string? currentVersion, string? webhookType = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Error message must not be empty.", nameof(message));

        var payload = CreateBasePayload($"{message}\nVersion: {currentVersion}", currentVersion, true);
        payload.WebhookType = webhookType ?? "AppLogs";

        return SendAsync(payload, cancellationToken);
    }

    public Task SendRawAsync(WebhookRequest payload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (string.IsNullOrWhiteSpace(payload.Application))
            payload.Application = _appName;

        payload.Os ??= TryGetOsDescription();
        return SendAsync(payload, cancellationToken);
    }

    private WebhookRequest CreateBasePayload(string message, string? version, bool isError)
    {
        return new WebhookRequest
        {
            Application = _appName,
            Message = message,
            Version = string.IsNullOrWhiteSpace(version) ? null : version.Trim(),
            Os = TryGetOsDescription(),
            IsError = isError
        };
    }

    private async Task SendAsync(WebhookRequest payload, CancellationToken cancellationToken)
    {
        var response =
            await _httpClient.PostAsJsonAsync(EndpointPath, payload, cancellationToken).ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
            return;

        var error = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var normalized = NormalizeForLog(error);
        throw new HttpRequestException(
            $"Haku webhook request failed with status {(int)response.StatusCode}: {normalized}");
    }

    private static string TryGetOsDescription()
    {
        try
        {
            return RuntimeInformation.OSDescription;
        }
        catch
        {
            return "Unknown OS";
        }
    }

    private static string NormalizeForLog(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return string.Empty;

        var normalized = message.ReplaceLineEndings(" ").Trim();
        if (normalized.Length <= 200)
            return normalized;

        var builder = new StringBuilder();
        builder.Append(normalized.AsSpan(0, 200));
        builder.Append("...");
        return builder.ToString();
    }
}

internal static class HakuWebhookClientProvider
{
    private const string ApplicationName = "EasyExtract";
    private const string UserAgent = "EasyExtractCrossPlatform-WebhookClient";

    private static readonly Lazy<HakuWebhookClient> LazyClient =
        new(CreateClient, LazyThreadSafetyMode.ExecutionAndPublication);

    public static HakuWebhookClient Instance => LazyClient.Value;

    private static HakuWebhookClient CreateClient()
    {
        var httpClient = new HttpClient
        {
            BaseAddress = new Uri(HakuWebhookClient.ApiBaseUrl),
            Timeout = TimeSpan.FromSeconds(10)
        };

        try
        {
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        }
        catch
        {
            // It's safe to continue without a custom user agent.
        }

        return new HakuWebhookClient(httpClient, ApplicationName);
    }
}