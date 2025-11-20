using System.Text.Json;

namespace EasyExtractCrossPlatform.Services;

public sealed partial class UpdateService
{
    public async Task<UpdateCheckResult> CheckForUpdatesAsync(UpdateSettings settings, Version? currentVersion,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var updateCheckScope =
            LoggingService.BeginPerformanceScope("CheckForUpdates", "Updater");

        var owner = string.IsNullOrWhiteSpace(settings.RepoOwner)
            ? "HakuSystems"
            : settings.RepoOwner.Trim();
        var repo = string.IsNullOrWhiteSpace(settings.RepoName)
            ? "EasyExtractUnitypackage"
            : settings.RepoName.Trim();

        var currentVersionLabel = currentVersion?.ToString() ?? "unknown";
        LoggingService.LogInformation(
            $"CheckForUpdates: requesting latest release | owner={owner} | repo={repo} | currentVersion={currentVersionLabel}");

        var requestUri = new Uri($"https://api.github.com/repos/{owner}/{repo}/releases/latest");
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.UserAgent.ParseAdd(UserAgent);
        request.Headers.Accept.ParseAdd("application/vnd.github+json");

        using var response = await HttpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var reason = $"{(int)response.StatusCode} {response.ReasonPhrase}".Trim();
            LoggingService.LogError($"CheckForUpdates: GitHub request failed | reason={reason}");
            return new UpdateCheckResult(false, null, $"GitHub responded with {reason}.");
        }

        await using var responseStream =
            await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

        GitHubRelease? release;
        try
        {
            release = await JsonSerializer
                .DeserializeAsync<GitHubRelease>(responseStream, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (JsonException jsonEx)
        {
            LoggingService.LogError("CheckForUpdates: failed to parse release information.", jsonEx);
            return new UpdateCheckResult(false, null, $"Failed to parse release information: {jsonEx.Message}");
        }

        if (release is null)
        {
            LoggingService.LogError("CheckForUpdates: GitHub returned no release information.");
            return new UpdateCheckResult(false, null, "No release information returned by GitHub.");
        }

        LoggingService.LogInformation(
            $"CheckForUpdates: release metadata received | tag={release.TagName ?? "unknown"} | draft={release.Draft} | prerelease={release.Prerelease} | published={release.PublishedAt?.ToString("O") ?? "n/a"}");

        if (release.Draft)
        {
            LoggingService.LogInformation(
                $"CheckForUpdates: skipping draft release | tag={release.TagName ?? "unknown"}");
            return new UpdateCheckResult(false, null, "Latest release is marked as draft.");
        }

        var normalizedTag = OperatingSystemInfo.NormalizeVersionTag(release.TagName);
        if (!Version.TryParse(normalizedTag, out var latestVersion))
        {
            LoggingService.LogError($"CheckForUpdates: failed to parse release tag '{release.TagName}'.");
            return new UpdateCheckResult(false, null, $"Unable to parse release tag '{release.TagName}'.");
        }

        if (currentVersion is not null && latestVersion <= currentVersion)
        {
            var message = currentVersion is null
                ? "No update available."
                : $"Version {currentVersion} is current.";
            LoggingService.LogInformation(
                $"CheckForUpdates: no update needed | latest={latestVersion} | current={currentVersion}");
            return new UpdateCheckResult(false, null, message);
        }

        var platform = OperatingSystemInfo.GetCurrentPlatform();
        var architectureToken = OperatingSystemInfo.GetArchitectureToken();
        LoggingService.LogInformation(
            $"CheckForUpdates: runtime context | platform={platform} | architecture={architectureToken}");

        if (!TrySelectAsset(release, platform, architectureToken, out var asset, out var assetMessage))
        {
            if (IsLegacyWindowsOnlyRelease(release, latestVersion, platform))
            {
                var tag = release.TagName ?? latestVersion.ToString();
                LoggingService.LogInformation(
                    $"CheckForUpdates: legacy Windows-only release detected | tag='{tag}' | platform={platform}");
                return new UpdateCheckResult(false, null, WindowsOnlyUpdateMessage);
            }

            LoggingService.LogError($"CheckForUpdates: asset selection failed | reason={assetMessage}");
            return new UpdateCheckResult(false, null, assetMessage);
        }

        if (asset?.BrowserDownloadUrl is null)
        {
            LoggingService.LogError("CheckForUpdates: matched update asset is missing a download URL.");
            return new UpdateCheckResult(false, null, "Matched asset is missing a download URL.");
        }

        ReleaseAssetInfo assetInfo;
        try
        {
            assetInfo = new ReleaseAssetInfo(
                asset.Name ?? $"EasyExtract-{normalizedTag}",
                new Uri(asset.BrowserDownloadUrl, UriKind.Absolute),
                asset.Size,
                asset.ContentType);
        }
        catch (Exception ex)
        {
            LoggingService.LogError("CheckForUpdates: failed to prepare release asset metadata.", ex);
            return new UpdateCheckResult(false, null, $"Failed to prepare asset metadata: {ex.Message}");
        }

        var manifest = new UpdateManifest(
            owner,
            repo,
            release.TagName ?? normalizedTag,
            latestVersion,
            release.Name,
            release.Body,
            release.PublishedAt,
            assetInfo,
            Uri.TryCreate(release.HtmlUrl, UriKind.Absolute, out var htmlUri) ? htmlUri : null);

        LoggingService.LogInformation(
            $"CheckForUpdates: update available | version={latestVersion} | asset='{assetInfo.Name}' | size={assetInfo.Size} bytes");

        return new UpdateCheckResult(true, manifest);
    }


    private static bool IsLegacyWindowsOnlyRelease(GitHubRelease release, Version latestVersion,
        RuntimePlatform platform)
    {
        if (platform == RuntimePlatform.Windows)
            return false;

        if (latestVersion > LegacyWindowsOnlyMaxVersion)
            return false;

        if (release.Assets is null || release.Assets.Count == 0)
            return false;

        foreach (var asset in release.Assets)
        {
            if (string.IsNullOrWhiteSpace(asset.Name))
                continue;

            var lowered = asset.Name.Trim().ToLowerInvariant();

            if (LegacyWindowsOnlyExtensions.Any(ext => lowered.EndsWith(ext, StringComparison.Ordinal)))
                return true;

            if (LegacyWindowsOnlyNameHints.Any(hint => lowered.Contains(hint, StringComparison.Ordinal)))
                return true;
        }

        return false;
    }


    private static bool TrySelectAsset(GitHubRelease release, RuntimePlatform platform, string architectureToken,
        out GitHubReleaseAsset? asset, out string message)
    {
        asset = null;

        if (release.Assets is null || release.Assets.Count == 0)
        {
            message = "Latest release does not contain any downloadable assets.";
            return false;
        }

        var platformToken = OperatingSystemInfo.GetPlatformToken(platform);
        var architectureCandidates = ArchitectureAliases.TryGetValue(architectureToken, out var aliases)
            ? aliases
            : new[] { architectureToken };

        var rankedAssets = release.Assets
            .Where(a => !string.IsNullOrWhiteSpace(a.Name))
            .Select(a => (Asset: a, Weight: ComputeAssetWeight(a.Name!, platformToken, architectureCandidates)))
            .Where(tuple => tuple.Weight > 0)
            .OrderByDescending(tuple => tuple.Weight)
            .ThenBy(tuple => tuple.Asset.Size == 0 ? long.MaxValue : tuple.Asset.Size)
            .ToList();

        if (rankedAssets.Count == 0)
        {
            message =
                $"No assets matched platform '{platformToken}' and architecture '{architectureToken}'. Ensure assets follow the EasyExtract-{release.TagName}-{{platform}}-{{arch}}.{{zip|tar.gz}} pattern.";
            return false;
        }

        asset = rankedAssets[0].Asset;
        LoggingService.LogInformation(
            $"Selected update asset '{asset.Name}' for platform '{platformToken}' and architecture '{architectureToken}'.");
        message = string.Empty;
        return true;
    }


    private static int ComputeAssetWeight(string assetName, string platformToken,
        IReadOnlyCollection<string> architectureAliases)
    {
        var lowered = assetName.ToLowerInvariant();

        if (!(lowered.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
              lowered.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) ||
              lowered.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase)))
            return 0;

        if (!lowered.Contains(platformToken, StringComparison.Ordinal))
            return 0;

        var weight = 1;

        foreach (var alias in architectureAliases)
        {
            if (string.IsNullOrWhiteSpace(alias))
                continue;

            if (lowered.Contains(alias.ToLowerInvariant(), StringComparison.Ordinal))
            {
                weight += 2;
                break;
            }
        }

        if (lowered.Contains("installer", StringComparison.Ordinal))
            weight--;

        return weight;
    }

}
