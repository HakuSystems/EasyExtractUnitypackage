using System.Net.Http.Json;
using System.Text.Json;

namespace EasyExtractCrossPlatform.Services;

internal sealed class HakuAnonymousSessionTokenProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly string _bootstrapPath;
    private readonly HttpClient _httpClient;
    private readonly string[] _scopes;
    private readonly SemaphoreSlim _syncRoot = new(1, 1);
    private string? _cachedDeviceId;
    private DateTimeOffset _expiresAtUtc;
    private string? _token;

    public HakuAnonymousSessionTokenProvider(HttpClient httpClient, string bootstrapPath, params string[] scopes)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _bootstrapPath = string.IsNullOrWhiteSpace(bootstrapPath)
            ? throw new ArgumentException("Bootstrap path must not be empty.", nameof(bootstrapPath))
            : bootstrapPath.TrimStart('/');
        _scopes = scopes
            .Where(scope => !string.IsNullOrWhiteSpace(scope))
            .Select(scope => scope.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<string?> GetAuthorizationHeaderValueAsync(string? deviceId,
        CancellationToken cancellationToken = default)
    {
        if (!TryNormalizeDeviceId(deviceId, out var normalizedDeviceId))
            return null;

        if (HasUsableToken(normalizedDeviceId))
            return $"Bearer {_token}";

        await _syncRoot.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (HasUsableToken(normalizedDeviceId))
                return $"Bearer {_token}";

            using var response = await _httpClient.PostAsJsonAsync(_bootstrapPath,
                new AnonymousSessionRequest
                {
                    DeviceId = normalizedDeviceId,
                    Scopes = _scopes
                }, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                throw new HttpRequestException(
                    $"Anonymous session bootstrap failed with status {(int)response.StatusCode}: {NormalizeForLog(error)}");
            }

            var payload = await response.Content
                .ReadFromJsonAsync<ApiEnvelope<AnonymousSessionResponse>>(JsonOptions, cancellationToken)
                .ConfigureAwait(false);

            var data = payload?.Data;
            if (string.IsNullOrWhiteSpace(data?.Token) || data.ExpiresAtUtc == default)
                throw new InvalidOperationException("Anonymous session bootstrap returned an invalid payload.");

            _token = data.Token;
            _cachedDeviceId = normalizedDeviceId;
            _expiresAtUtc = data.ExpiresAtUtc;
            return $"Bearer {_token}";
        }
        finally
        {
            _syncRoot.Release();
        }
    }

    private bool HasUsableToken(string normalizedDeviceId)
    {
        return string.Equals(_cachedDeviceId, normalizedDeviceId, StringComparison.OrdinalIgnoreCase) &&
               !string.IsNullOrWhiteSpace(_token) &&
               _expiresAtUtc > DateTimeOffset.UtcNow.AddMinutes(1);
    }

    private static bool TryNormalizeDeviceId(string? deviceId, out string normalizedDeviceId)
    {
        if (Guid.TryParse(deviceId?.Trim(), out var parsedDeviceId))
        {
            normalizedDeviceId = parsedDeviceId.ToString("D");
            return true;
        }

        normalizedDeviceId = string.Empty;
        return false;
    }

    private static string NormalizeForLog(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return string.Empty;

        var normalized = message.ReplaceLineEndings(" ").Trim();
        return normalized.Length <= 200
            ? normalized
            : $"{normalized[..200]}...";
    }

    private sealed class AnonymousSessionRequest
    {
        public string DeviceId { get; set; } = string.Empty;
        public string[] Scopes { get; set; } = [];
    }

    private sealed class ApiEnvelope<T>
    {
        public string? Status { get; set; }
        public string? Message { get; set; }
        public T? Data { get; set; }
    }

    private sealed class AnonymousSessionResponse
    {
        public string Token { get; set; } = string.Empty;
        public string DeviceId { get; set; } = string.Empty;
        public DateTimeOffset ExpiresAtUtc { get; set; }
        public string[] Scopes { get; set; } = [];
    }
}