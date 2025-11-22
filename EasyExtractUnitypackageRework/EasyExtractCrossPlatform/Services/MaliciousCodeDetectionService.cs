using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace EasyExtractCrossPlatform.Services;

public interface IMaliciousCodeDetectionService
{
    Task<MaliciousCodeScanResult> ScanUnityPackageAsync(
        string packagePath,
        CancellationToken cancellationToken = default);
}

public sealed partial class MaliciousCodeDetectionService : IMaliciousCodeDetectionService
{
    private const long MaxScannableBytes = 4 * 1024 * 1024;
    private const int MaxMatchesPerPatternPerFile = 25;

    private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(5);

    private static readonly Regex DiscordWebhookRegex = new(
        @"https:\/\/discord(?:app)?\.com\/api\/webhooks\/\d{18}\/[A-Za-z0-9\-_]{68}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex LinkDetectionRegex = new(
        @"https?:\/\/(?:www\.)?[-a-zA-Z0-9@:%._\+~#=]{1,256}\.[a-z]{2,63}\b(?:[-a-zA-Z0-9@:%_\+.~#?&//=]*)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex[] SuspiciousPatterns =
    {
        new(@"UnityWebRequest", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"HttpClient", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"WebClient", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"HttpWebRequest", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"RestClient", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"WWW\s*\(", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"File\.WriteAllText", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"File\.WriteAllBytes", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"File\.Delete", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"Directory\.Delete", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"Process\.Start", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"Registry\.", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"RegistryKey", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"Application\.persistentDataPath",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"Application\.dataPath", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"Environment\.GetFolderPath",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"System\.Reflection", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"Assembly\.Load", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"Activator\.CreateInstance",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"Convert\.FromBase64String",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"Encoding\.UTF8\.GetString",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"System\.Text\.Encoding", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
    };

    private static readonly HashSet<string> ScannableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".js", ".boo", ".shader", ".cginc", ".hlsl",
        ".txt", ".json", ".xml", ".yaml", ".yml",
        ".asset", ".unity", ".prefab", ".mat",
        ".asmdef", ".asmref"
    };

    private static readonly string[] AllowedDomains =
    {
        "unity3d.com",
        "unity.com",
        "assetstore.unity3d.com",
        "connect.unity.com",
        "developer.unity3d.com",
        "docs.unity3d.com",
        "forum.unity.com",
        "id.unity.com",
        "learn.unity.com",
        "support.unity3d.com",
        "microsoft.com",
        "docs.microsoft.com",
        "dotnet.microsoft.com",
        "nuget.org",
        "visualstudio.com",
        "azure.microsoft.com",
        "github.com",
        "gitlab.com",
        "bitbucket.org",
        "sourceforge.net",
        "codeplex.com",
        "npmjs.com",
        "npmjs.org",
        "yarnpkg.com",
        "packagist.org",
        "pypi.org",
        "stackoverflow.com",
        "stackexchange.com",
        "developer.mozilla.org",
        "w3schools.com",
        "tutorialspoint.com",
        "gamedev.net",
        "gamasutra.com",
        "indiedb.com",
        "moddb.com",
        "itch.io",
        "freesound.org",
        "opengameart.org",
        "kenney.nl",
        "mixamo.com",
        "sketchfab.com",
        "amazonaws.com",
        "googlecloud.com",
        "firebase.google.com",
        "heroku.com",
        "netlify.com",
        "vercel.com",
        "cloudflare.com",
        "jsdelivr.net",
        "unpkg.com",
        "cdnjs.cloudflare.com",
        "maxcdn.bootstrapcdn.com",
        "google.com",
        "googleapis.com",
        "googletagmanager.com",
        "google-analytics.com",
        "fonts.googleapis.com",
        "fonts.gstatic.com",
        "discord.gg",
        "reddit.com",
        "twitter.com",
        "youtube.com",
        "twitch.tv",
        "edu",
        "ac.uk",
        "mit.edu",
        "stanford.edu",
        "coursera.org",
        "udemy.com",
        "edx.org",
        "apache.org",
        "mozilla.org",
        "gnu.org",
        "fsf.org",
        "opensource.org",
        "w3.org",
        "whatwg.org",
        "ietf.org",
        "iso.org",
        "jetbrains.com",
        "atlassian.com",
        "slack.com",
        "trello.com",
        "notion.so",
        "letsencrypt.org",
        "digicert.com",
        "symantec.com",
        "verisign.com"
    };

    private readonly ConcurrentDictionary<string, Task<MaliciousCodeScanResult>> _inFlightScans =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<string, CachedScanResult> _scanCache = new(StringComparer.OrdinalIgnoreCase);
}