using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
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

    public static async Task<IReadOnlyList<GitCommitInfo>> GetRecentCommitsAsync(
        int count = 30,
        CancellationToken cancellationToken = default)
    {
        var perPage = Math.Clamp(count, 1, 100);
        var endpoint = $"repos/{RepoOwner}/{RepoName}/commits?per_page={perPage}";

        using var response = await HttpClient.GetAsync(endpoint, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var reason = $"{(int)response.StatusCode} {response.ReasonPhrase}".Trim();
            throw new HttpRequestException($"GitHub responded with {reason} while requesting commits.");
        }

        await using var responseStream =
            await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var payload =
            await JsonSerializer.DeserializeAsync<List<GitHubCommitDto>>(responseStream, JsonOptions, cancellationToken)
                .ConfigureAwait(false);

        if (payload is null || payload.Count == 0)
            return Array.Empty<GitCommitInfo>();

        var result = new List<GitCommitInfo>(payload.Count);
        foreach (var commit in payload)
        {
            if (commit?.Commit is null || string.IsNullOrWhiteSpace(commit.Sha))
                continue;

            var message = commit.Commit.Message ?? string.Empty;
            var normalizedMessage = NormalizeLineEndings(message).TrimEnd();
            var (title, description) = ExtractTitleAndDescription(normalizedMessage, commit.Sha);

            var date = commit.Commit.Author?.Date ??
                       commit.Commit.Committer?.Date ??
                       DateTimeOffset.MinValue;

            var author = commit.Commit.Author?.Name ??
                         commit.Commit.Committer?.Name ??
                         "Unknown author";

            var commitUrl = $"https://github.com/{RepoOwner}/{RepoName}/commit/{commit.Sha}";

            result.Add(new GitCommitInfo(
                commit.Sha,
                title,
                description,
                author,
                date,
                commitUrl));
        }

        return result;
    }

    private static string NormalizeLineEndings(string value)
    {
        return value.Replace("\r\n", "\n")
            .Replace('\r', '\n');
    }

    private static (string Title, string? Description) ExtractTitleAndDescription(string message, string fallbackTitle)
    {
        if (string.IsNullOrWhiteSpace(message))
            return (fallbackTitle, null);

        var newlineIndex = message.IndexOf('\n');
        if (newlineIndex < 0)
        {
            var singleLineTitle = NormalizeCommitTitle(message);
            return (string.IsNullOrWhiteSpace(singleLineTitle) ? fallbackTitle : singleLineTitle, null);
        }

        var rawTitle = message[..newlineIndex];
        var remainder = message[(newlineIndex + 1)..];

        var normalizedTitle = NormalizeCommitTitle(rawTitle);
        if (string.IsNullOrWhiteSpace(normalizedTitle))
            normalizedTitle = fallbackTitle;

        var description = remainder.Trim('\n');
        if (string.IsNullOrWhiteSpace(description))
            description = null;

        return (normalizedTitle, description);
    }

    private static string NormalizeCommitTitle(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var trimmed = value.Trim();

        if (trimmed.Length >= 4)
            if ((trimmed.StartsWith("**") && trimmed.EndsWith("**")) ||
                (trimmed.StartsWith("__") && trimmed.EndsWith("__")))
                trimmed = trimmed[2..^2].Trim();

        return trimmed;
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient
        {
            BaseAddress = BaseUri,
            Timeout = TimeSpan.FromSeconds(15)
        };

        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "EasyExtractCrossPlatform/2.0 (+https://github.com/HakuSystems/EasyExtractUnitypackage)");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

        return client;
    }

    private sealed record GitHubCommitDto(string Sha, GitCommitDto Commit);

    private sealed record GitCommitDto(GitCommitAuthorDto? Author, GitCommitAuthorDto? Committer, string? Message);

    private sealed record GitCommitAuthorDto(string? Name, DateTimeOffset? Date);
}