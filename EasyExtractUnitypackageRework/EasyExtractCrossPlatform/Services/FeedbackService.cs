namespace EasyExtractCrossPlatform.Services;

public static class FeedbackService
{
    public static async Task SendFeedbackAsync(string feedback, string? version,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(feedback))
            throw new ArgumentException("Feedback must not be empty.", nameof(feedback));

        var trimmedFeedback = feedback.Trim();
        if (trimmedFeedback.Length > 1800)
            trimmedFeedback = trimmedFeedback[..1800] + "...";

        var versionLabel = string.IsNullOrWhiteSpace(version)
            ? null
            : version.Trim();

        LoggingService.LogInformation(
            $"Sending feedback payload via HakuApi (length={trimmedFeedback.Length}, version={versionLabel ?? "unknown"}).");

        try
        {
            var client = HakuWebhookClientProvider.Instance;
            await client.SendFeedbackAsync(trimmedFeedback, versionLabel, cancellationToken).ConfigureAwait(false);
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
            LoggingService.LogError("Failed to send feedback via HakuApi webhook.", ex);
            throw;
        }
    }
}