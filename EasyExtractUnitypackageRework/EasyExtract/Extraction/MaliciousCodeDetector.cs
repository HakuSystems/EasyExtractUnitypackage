using System.Text.RegularExpressions;

namespace EasyExtract.Extraction;

public class MaliciousCodeDetector
{
    private static readonly Regex DiscordWebhookRegex =
        new(@"https:\/\/discord(?:app)?\.com\/api\/webhooks\/\d{18}\/[A-Za-z0-9-_]{68}",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex LinkDetectionRegex = new(
        @"https?:\/\/(?:www\.)?[-a-zA-Z0-9@:%._\+~#=]{2,256}\.[a-z]{2,6}\b([-a-zA-Z0-9@:%_\+.~#?&//=]*)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex Base64Regex = new(@"[A-Za-z0-9+/]{20,}", RegexOptions.Compiled);


    /// <summary>
    ///     Starts an asynchronous scan for Discord webhook URLs in the content of a specified code file.
    /// </summary>
    /// <param name="lineOfCode">The line of code to scan for Discord webhook URLs.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains a boolean value indicating whether
    ///     any Discord webhook URLs were found in the code file content.
    /// </returns>
    public Task<bool> StartDiscordWebhookScanAsync(string lineOfCode)
    {
        return Task.FromResult(DiscordWebhookRegex.IsMatch(lineOfCode));
    }

    /// <summary>
    ///     Starts an asynchronous scan for link detection in the content of a specified code file.
    /// </summary>
    /// <param name="lineOfCode">The line of code to scan for links.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains a boolean value indicating whether
    ///     any links were found in the code file content.
    /// </returns>
    public Task<bool> StartLinkDetectionAsync(string lineOfCode)
    {
        return Task.FromResult(LinkDetectionRegex.IsMatch(lineOfCode));
    }

    /// Starts an asynchronous scan for obfuscation detection in the content of a specified code file.
    /// </summary>
    /// <param name="lineOfCode">The line of code to scan for obfuscation detection.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains a boolean value indicating whether
    ///     any obfuscated code was found in the code file content.
    /// </returns>
    public Task<bool> StartBase64DetectionAsync(string lineOfCode)
    {
        return Task.FromResult(Base64Regex.IsMatch(lineOfCode));
    }
}