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
    internal const string ApiBaseUrl = "https://easyextract.net/";
    private const string EndpointPath = "api/haku/webhooks/send";
    private readonly string _appName;
    private readonly Func<string?> _deviceIdProvider;
    private readonly HttpClient _httpClient;
    private readonly HakuAnonymousSessionTokenProvider _tokenProvider;

    public HakuWebhookClient(HttpClient httpClient, string appName, Func<string?>? deviceIdProvider = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _appName = string.IsNullOrWhiteSpace(appName)
            ? throw new ArgumentException("Application name must not be empty.", nameof(appName))
            : appName;
        _deviceIdProvider = deviceIdProvider ?? ResolveDeviceIdFromSettings;
        _tokenProvider = new HakuAnonymousSessionTokenProvider(_httpClient, "api/haku/v1/auth/anonymous-session",
            "webhook:send");

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
        return SendErrorReportAsync(exception.Message, currentVersion, cancellationToken: cancellationToken);
    }

    public Task SendErrorReportAsync(string message, string? currentVersion, string? webhookType = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Error message must not be empty.", nameof(message));

        var payload = CreateBasePayload(message, currentVersion, true);
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
        var authorizationHeader = await _tokenProvider
            .GetAuthorizationHeaderValueAsync(_deviceIdProvider(), cancellationToken)
            .ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(authorizationHeader))
            throw new InvalidOperationException("A valid device identifier is required to send webhook requests.");

        using var request = new HttpRequestMessage(HttpMethod.Post, EndpointPath);
        request.Headers.TryAddWithoutValidation("Authorization", authorizationHeader);
        request.Content = JsonContent.Create(payload);

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

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

    private static string? ResolveDeviceIdFromSettings()
    {
        try
        {
            return AppSettingsService.Load().DeviceId;
        }
        catch
        {
            return null;
        }
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
        }

        return new HakuWebhookClient(httpClient, ApplicationName);
    }
}