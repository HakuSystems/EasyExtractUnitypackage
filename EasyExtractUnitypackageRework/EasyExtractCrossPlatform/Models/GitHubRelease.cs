using System.Text.Json.Serialization;

namespace EasyExtractCrossPlatform.Models;

public sealed class GitHubRelease
{
    [JsonPropertyName("tag_name")] public string? TagName { get; set; }

    [JsonPropertyName("name")] public string? Name { get; set; }

    [JsonPropertyName("draft")] public bool Draft { get; set; }

    [JsonPropertyName("prerelease")] public bool Prerelease { get; set; }

    [JsonPropertyName("body")] public string? Body { get; set; }

    [JsonPropertyName("html_url")] public string? HtmlUrl { get; set; }

    [JsonPropertyName("published_at")] public DateTimeOffset? PublishedAt { get; set; }

    [JsonPropertyName("assets")] public List<GitHubReleaseAsset> Assets { get; set; } = new();
}

public sealed class GitHubReleaseAsset
{
    [JsonPropertyName("name")] public string? Name { get; set; }

    [JsonPropertyName("browser_download_url")]
    public string? BrowserDownloadUrl { get; set; }

    [JsonPropertyName("size")] public long Size { get; set; }

    [JsonPropertyName("content_type")] public string? ContentType { get; set; }
}