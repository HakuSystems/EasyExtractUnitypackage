using System;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace EasyExtractCrossPlatform.Services;

public static class FeedbackService
{
    private const string WebhookUrl =
        "https://discord.com/api/webhooks/1278449395431637066/Z3HwKW5Z4omiOugfz0zKPPoheaaYFG-3J1s6caEsx1mGISrLOqSc1sOuVVD5in6crmzM";

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

        var environmentLabel = GetEnvironmentLabel();
        var timestamp = DateTimeOffset.UtcNow;

        LoggingService.LogInformation(
            $"Sending feedback payload (length={trimmedFeedback.Length}, version={versionLabel}).");

        try
        {
            var payload = new
            {
                content = $"**New feedback (v {versionLabel})**",
                allowed_mentions = new
                {
                    parse = Array.Empty<string>()
                },
                embeds = new[]
                {
                    new
                    {
                        description = trimmedFeedback,
                        timestamp,
                        fields = new[]
                        {
                            new
                            {
                                name = "App version",
                                value = versionLabel,
                                inline = true
                            },
                            new
                            {
                                name = "OS",
                                value = environmentLabel,
                                inline = true
                            }
                        }
                    }
                }
            };

            await DiscordWebhookNotifier.SendPayloadAsync(WebhookUrl, payload, cancellationToken).ConfigureAwait(false);
            LoggingService.LogInformation("Feedback sent successfully.");
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            var timeoutMessage = "Feedback request timed out while contacting the feedback server.";
            LoggingService.LogError(timeoutMessage, ex);
            throw new HttpRequestException(timeoutMessage, ex);
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Failed to send feedback to Discord webhook.", ex);
            throw;
        }
    }

    private static string GetEnvironmentLabel()
    {
        try
        {
            return RuntimeInformation.OSDescription;
        }
        catch
        {
            return "unknown";
        }
    }
}