using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace EasyExtractCrossPlatform.Services;

public static class FeedbackService
{
    private const string WebhookUrl =
        "https://discord.com/api/webhooks/1278449395431637066/Z3HwKW5Z4omiOugfz0zKPPoheaaYFG-3J1s6caEsx1mGISrLOqSc1sOuVVD5in6crmzM";

    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    public static async Task SendFeedbackAsync(string feedback, string? version,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(feedback))
            throw new ArgumentException("Feedback must not be empty.", nameof(feedback));

        var trimmedFeedback = feedback.Trim();
        if (trimmedFeedback.Length > 1800) trimmedFeedback = trimmedFeedback[..1800] + "...";

        var versionLabel = string.IsNullOrWhiteSpace(version)
            ? "unknown"
            : version.Trim();

        LoggingService.LogInformation(
            $"Sending feedback payload (length={trimmedFeedback.Length}, version={versionLabel}).");

        try
        {
            var payload = new
            {
                content = $"**New feedback (v {versionLabel})**{Environment.NewLine}{trimmedFeedback}",
                allowed_mentions = new
                {
                    parse = Array.Empty<string>()
                }
            };

            var json = JsonSerializer.Serialize(payload);
            using var request = new HttpRequestMessage(HttpMethod.Post, WebhookUrl)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            using var response = await HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                var message =
                    $"Discord webhook responded with status {(int)response.StatusCode}: {body}";
                LoggingService.LogError(message);
                throw new HttpRequestException(message);
            }

            LoggingService.LogInformation("Feedback sent successfully.");
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Failed to send feedback to Discord webhook.", ex);
            throw;
        }
    }
}