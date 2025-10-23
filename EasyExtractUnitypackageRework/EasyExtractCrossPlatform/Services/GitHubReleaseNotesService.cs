using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using EasyExtractCrossPlatform.Models;

namespace EasyExtractCrossPlatform.Services;

public static class GitHubReleaseNotesService
{
    private const string RepoOwner = "HakuSystems";
    private const string RepoName = "EasyExtractUnitypackage";
    private static readonly Uri BaseUri = new("https://api.github.com/");

    private static readonly HttpClient HttpClient = CreateClient();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task<IReadOnlyList<GitReleaseInfo>> GetRecentReleasesAsync(
        int count = 10,
        CancellationToken cancellationToken = default)
    {
        var perPage = Math.Clamp(count, 1, 100);
        var (releases, _) = await FetchReleasesPageAsync(perPage, 1, cancellationToken).ConfigureAwait(false);
        if (releases.Count > count)
            releases.RemoveRange(count, releases.Count - count);

        return releases;
    }

    public static async Task<IReadOnlyList<GitReleaseInfo>> GetAllReleasesAsync(
        CancellationToken cancellationToken = default)
    {
        const int perPage = 100;
        var page = 1;
        var results = new List<GitReleaseInfo>();

        while (true)
        {
            var (pageReleases, hasNextPage) =
                await FetchReleasesPageAsync(perPage, page, cancellationToken).ConfigureAwait(false);

            if (pageReleases.Count == 0)
                break;

            results.AddRange(pageReleases);

            if (!hasNextPage)
                break;

            page++;
        }

        return results;
    }

    private static async Task<(List<GitReleaseInfo> Releases, bool HasNextPage)> FetchReleasesPageAsync(
        int perPage,
        int page,
        CancellationToken cancellationToken)
    {
        var endpoint = $"repos/{RepoOwner}/{RepoName}/releases?per_page={perPage}&page={page}";

        using var response = await HttpClient.GetAsync(endpoint, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var reason = $"{(int)response.StatusCode} {response.ReasonPhrase}".Trim();
            throw new HttpRequestException($"GitHub responded with {reason} while requesting releases.");
        }

        await using var responseStream =
            await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var payload =
            await JsonSerializer
                .DeserializeAsync<List<GitHubReleaseDto>>(responseStream, JsonOptions, cancellationToken)
                .ConfigureAwait(false);

        var releases = BuildReleaseInfos(payload);
        var hasNextPage = releases.Count > 0 && ResponseHasNextPage(response);

        return (releases, hasNextPage);
    }

    private static List<GitReleaseInfo> BuildReleaseInfos(List<GitHubReleaseDto>? payload)
    {
        if (payload is null || payload.Count == 0)
            return new List<GitReleaseInfo>();

        var results = new List<GitReleaseInfo>(payload.Count);
        foreach (var release in payload)
        {
            if (release is null || string.IsNullOrWhiteSpace(release.TagName) ||
                string.IsNullOrWhiteSpace(release.HtmlUrl))
                continue;

            var assets = new List<GitReleaseAsset>();
            if (release.Assets is { Count: > 0 })
            {
                foreach (var asset in release.Assets)
                {
                    if (asset?.BrowserDownloadUrl is null || string.IsNullOrWhiteSpace(asset.Name))
                        continue;

                    assets.Add(new GitReleaseAsset(
                        asset.Id,
                        asset.Name,
                        asset.Size,
                        asset.BrowserDownloadUrl,
                        asset.ContentType));
                }
            }

            var authorName = release.Author?.Login ?? release.Author?.Name ?? "Unknown author";

            results.Add(new GitReleaseInfo(
                release.Id,
                release.Name,
                release.TagName,
                authorName,
                release.PublishedAt,
                release.Body,
                release.HtmlUrl,
                release.Draft,
                release.Prerelease,
                assets));
        }

        return results;
    }

    private static bool ResponseHasNextPage(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Link", out var linkValues))
            return false;

        foreach (var linkValue in linkValues)
        {
            if (linkValue is null)
                continue;

            if (linkValue.Contains("rel=\"next\"", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient
        {
            BaseAddress = BaseUri,
            Timeout = TimeSpan.FromSeconds(20)
        };

        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "EasyExtractCrossPlatform/2.0 (+https://github.com/HakuSystems/EasyExtractUnitypackage)");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

        return client;
    }

    private sealed record GitHubReleaseDto(
        [property: JsonPropertyName("id")] long Id,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("tag_name")]
        string TagName,
        [property: JsonPropertyName("html_url")]
        string HtmlUrl,
        [property: JsonPropertyName("body")] string? Body,
        [property: JsonPropertyName("draft")] bool Draft,
        [property: JsonPropertyName("prerelease")]
        bool Prerelease,
        [property: JsonPropertyName("published_at")]
        DateTimeOffset? PublishedAt,
        [property: JsonPropertyName("author")] GitHubAuthorDto? Author,
        [property: JsonPropertyName("assets")] List<GitHubAssetDto>? Assets);

    private sealed record GitHubAuthorDto(
        [property: JsonPropertyName("login")] string? Login,
        [property: JsonPropertyName("name")] string? Name);

    private sealed record GitHubAssetDto(
        [property: JsonPropertyName("id")] long Id,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("size")] long Size,
        [property: JsonPropertyName("content_type")]
        string? ContentType,
        [property: JsonPropertyName("browser_download_url")]
        string BrowserDownloadUrl);
}