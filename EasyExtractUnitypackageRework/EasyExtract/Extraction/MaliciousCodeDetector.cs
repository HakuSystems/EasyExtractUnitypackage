using System.Text.RegularExpressions;
using EasyExtract.Config;

namespace EasyExtract.Extraction;

public class MaliciousCodeDetector
{
    private static readonly Regex DiscordWebhookRegex =
        new(@"https:\/\/discord(?:app)?\.com\/api\/webhooks\/\d{18}\/[A-Za-z0-9-_]{68}",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly BetterLogger _logger = new();

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
}