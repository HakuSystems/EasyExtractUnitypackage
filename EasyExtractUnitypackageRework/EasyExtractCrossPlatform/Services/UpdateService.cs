using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EasyExtractCrossPlatform.Models;
using EasyExtractCrossPlatform.Utilities;

namespace EasyExtractCrossPlatform.Services;

public interface IUpdateService
{
    Task<UpdateCheckResult> CheckForUpdatesAsync(UpdateSettings settings, Version? currentVersion,
        CancellationToken cancellationToken = default);

    Task<UpdateInstallResult> DownloadAndPrepareUpdateAsync(UpdateManifest manifest,
        IProgress<UpdateProgress>? progress = null,
        CancellationToken cancellationToken = default);

    bool TryLaunchPreparedUpdate(UpdatePreparation preparation);
}

public sealed class UpdateService : IUpdateService
{
    private const string UserAgent =
        "EasyExtractCrossPlatform-Updater/1.0 (+https://github.com/HakuSystems/EasyExtractUnitypackage)";

    private const string WindowsOnlyUpdateMessage = "Windows-only update detected";

    private static readonly HttpClient HttpClient = new();

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly Version LegacyWindowsOnlyMaxVersion = new(2, 0, 7, 0);
    private static readonly string[] LegacyWindowsOnlyExtensions = { ".rar" };
    private static readonly string[] LegacyWindowsOnlyNameHints = { "easyextractpublish" };

    private static readonly Dictionary<string, string[]> ArchitectureAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        { "x64", new[] { "x64", "x86_64", "amd64" } },
        { "x86", new[] { "x86", "win32", "ia32" } },
        { "arm64", new[] { "arm64", "aarch64" } },
        { "arm", new[] { "arm", "armhf" } },
        { "s390x", new[] { "s390x" } }
    };

    public static UpdateService Instance { get; } = new();

    public async Task<UpdateCheckResult> CheckForUpdatesAsync(UpdateSettings settings, Version? currentVersion,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var owner = string.IsNullOrWhiteSpace(settings.RepoOwner)
            ? "HakuSystems"
            : settings.RepoOwner.Trim();
        var repo = string.IsNullOrWhiteSpace(settings.RepoName)
            ? "EasyExtractUnitypackage"
            : settings.RepoName.Trim();

        var currentVersionLabel = currentVersion?.ToString() ?? "unknown";
        LoggingService.LogInformation(
            $"Checking for updates from {owner}/{repo} (currentVersion={currentVersionLabel}).");

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
            LoggingService.LogError($"Update check failed: GitHub responded with {reason}.");
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
            LoggingService.LogError("Failed to parse release information from GitHub.", jsonEx);
            return new UpdateCheckResult(false, null, $"Failed to parse release information: {jsonEx.Message}");
        }

        if (release is null)
        {
            LoggingService.LogError("Update check failed: GitHub returned no release information.");
            return new UpdateCheckResult(false, null, "No release information returned by GitHub.");
        }

        if (release.Draft)
        {
            LoggingService.LogInformation("Latest release is a draft; update will be skipped.");
            return new UpdateCheckResult(false, null, "Latest release is marked as draft.");
        }

        var normalizedTag = OperatingSystemInfo.NormalizeVersionTag(release.TagName);
        if (!Version.TryParse(normalizedTag, out var latestVersion))
        {
            LoggingService.LogError($"Failed to parse release tag '{release.TagName}'.");
            return new UpdateCheckResult(false, null, $"Unable to parse release tag '{release.TagName}'.");
        }

        if (currentVersion is not null && latestVersion <= currentVersion)
        {
            var message = currentVersion is null
                ? "No update available."
                : $"Version {currentVersion} is current.";
            LoggingService.LogInformation($"No update available. Latest={latestVersion}, current={currentVersion}.");
            return new UpdateCheckResult(false, null, message);
        }

        var platform = OperatingSystemInfo.GetCurrentPlatform();
        var architectureToken = OperatingSystemInfo.GetArchitectureToken();

        if (!TrySelectAsset(release, platform, architectureToken, out var asset, out var assetMessage))
        {
            if (IsLegacyWindowsOnlyRelease(release, latestVersion, platform))
            {
                var tag = release.TagName ?? latestVersion.ToString();
                LoggingService.LogInformation(
                    $"Release '{tag}' is a legacy Windows-only update; skipping update for platform '{platform}'.");
                return new UpdateCheckResult(false, null, WindowsOnlyUpdateMessage);
            }

            LoggingService.LogError($"No suitable update asset found: {assetMessage}");
            return new UpdateCheckResult(false, null, assetMessage);
        }

        if (asset?.BrowserDownloadUrl is null)
        {
            LoggingService.LogError("Matched update asset is missing a download URL.");
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
            LoggingService.LogError("Failed to prepare release asset metadata.", ex);
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
            $"Update available: version={latestVersion}, asset='{assetInfo.Name}', size={assetInfo.Size} bytes.");

        return new UpdateCheckResult(true, manifest);
    }

    public async Task<UpdateInstallResult> DownloadAndPrepareUpdateAsync(UpdateManifest manifest,
        IProgress<UpdateProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            LoggingService.LogInformation(
                $"Preparing update {manifest.Version} from {manifest.Asset.DownloadUri}.");
            progress?.Report(new UpdateProgress(UpdatePhase.Downloading, 0.0));

            var updatesRoot = Path.Combine(AppSettingsService.SettingsDirectory, "Updates");
            Directory.CreateDirectory(updatesRoot);
            LoggingService.LogInformation($"Using updates root '{updatesRoot}'.");

            var versionDirectory = Path.Combine(updatesRoot, manifest.Version.ToString());
            if (Directory.Exists(versionDirectory))
                Directory.Delete(versionDirectory, true);
            Directory.CreateDirectory(versionDirectory);
            LoggingService.LogInformation($"Created working directory '{versionDirectory}'.");

            var archivePath = Path.Combine(versionDirectory, manifest.Asset.Name);
            await DownloadAssetAsync(manifest.Asset, archivePath, progress, cancellationToken).ConfigureAwait(false);
            LoggingService.LogInformation($"Update asset downloaded to '{archivePath}'.");

            cancellationToken.ThrowIfCancellationRequested();

            progress?.Report(new UpdateProgress(UpdatePhase.Extracting, null));

            var payloadDirectory = Path.Combine(versionDirectory, "payload");
            Directory.CreateDirectory(payloadDirectory);
            await ExtractArchiveAsync(archivePath, payloadDirectory, cancellationToken).ConfigureAwait(false);
            LoggingService.LogInformation($"Update payload extracted to '{payloadDirectory}'.");

            cancellationToken.ThrowIfCancellationRequested();

            progress?.Report(new UpdateProgress(UpdatePhase.Preparing, null));

            var contentRoot = ResolveContentRoot(payloadDirectory);
            var platform = OperatingSystemInfo.GetCurrentPlatform();
            var appName = OperatingSystemInfo.GetApplicationName();
            var executableRelativePath = ResolveExecutableRelativePath(contentRoot, platform, appName);
            var installDirectory = ResolveInstallDirectory();

            var scriptsDirectory = Path.Combine(versionDirectory, "scripts");
            Directory.CreateDirectory(scriptsDirectory);

            var scriptInfo = CreateUpdateScript(platform, scriptsDirectory, contentRoot, installDirectory,
                executableRelativePath, manifest.Version, appName);

            if (!OperatingSystem.IsWindows())
                EnsureUnixExecutable(scriptInfo.ScriptPath);

            progress?.Report(new UpdateProgress(UpdatePhase.Preparing, 1.0));
            LoggingService.LogInformation(
                $"Update preparation complete. Script='{scriptInfo.ScriptPath}', workingDirectory='{scriptInfo.WorkingDirectory}'.");

            var preparation = new UpdatePreparation
            {
                TargetVersion = manifest.Version,
                StageDirectory = versionDirectory,
                SourceDirectory = contentRoot,
                ScriptPath = scriptInfo.ScriptPath,
                ScriptArguments = scriptInfo.Arguments,
                WorkingDirectory = scriptInfo.WorkingDirectory,
                ReleaseName = manifest.ReleaseName,
                ReleaseNotes = manifest.ReleaseNotes
            };

            LoggingService.LogInformation(
                $"Update prepared successfully. StageDirectory='{preparation.StageDirectory}'.");
            return UpdateInstallResult.FromPreparation(preparation);
        }
        catch (OperationCanceledException)
        {
            LoggingService.LogInformation("Update preparation was cancelled.");
            return UpdateInstallResult.Failed("Update preparation was cancelled.");
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Failed to prepare update.", ex);
            return UpdateInstallResult.Failed("Failed to prepare update.", ex);
        }
    }

    public bool TryLaunchPreparedUpdate(UpdatePreparation preparation)
    {
        try
        {
            LoggingService.LogInformation(
                $"Launching prepared update script '{preparation.ScriptPath}' with arguments '{string.Join(' ', preparation.ScriptArguments)}'.");

            ProcessStartInfo startInfo;
            if (OperatingSystem.IsWindows())
            {
                startInfo = new ProcessStartInfo("cmd.exe")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = preparation.WorkingDirectory
                };

                startInfo.ArgumentList.Add("/c");
                startInfo.ArgumentList.Add(preparation.ScriptPath);
            }
            else
            {
                startInfo = new ProcessStartInfo("/bin/sh")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = preparation.WorkingDirectory
                };

                startInfo.ArgumentList.Add(preparation.ScriptPath);
            }

            foreach (var argument in preparation.ScriptArguments)
                startInfo.ArgumentList.Add(argument);

            using var process = Process.Start(startInfo);
            var launched = process is not null;
            LoggingService.LogInformation(
                launched
                    ? "Update script launch succeeded."
                    : "Update script launch returned null process handle.");
            return launched;
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Failed to launch update script.", ex);
            return false;
        }
    }

    private static async Task DownloadAssetAsync(ReleaseAssetInfo asset, string destinationPath,
        IProgress<UpdateProgress>? progress, CancellationToken cancellationToken)
    {
        LoggingService.LogInformation(
            $"Downloading update asset '{asset.Name}' ({asset.Size} bytes) to '{destinationPath}'.");

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, asset.DownloadUri);
            request.Headers.UserAgent.ParseAdd(UserAgent);
            request.Headers.Accept.ParseAdd("application/octet-stream");

            using var response = await HttpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            await using var responseStream =
                await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using var fileStream =
                new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);

            var buffer = new byte[81920];
            long totalRead = 0;
            while (true)
            {
                var read = await responseStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)
                    .ConfigureAwait(false);
                if (read == 0)
                    break;

                await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                totalRead += read;

                double? percentage = null;
                if (asset.Size > 0)
                    percentage = Math.Clamp((double)totalRead / asset.Size, 0, 1);

                progress?.Report(new UpdateProgress(UpdatePhase.Downloading, percentage, totalRead, asset.Size));
            }

            LoggingService.LogInformation(
                $"Download completed for '{asset.Name}'. BytesReceived={totalRead}, destination='{destinationPath}'.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LoggingService.LogError($"Failed to download update asset '{asset.Name}'.", ex);
            throw;
        }
    }

    private static async Task ExtractArchiveAsync(string archivePath, string destinationDirectory,
        CancellationToken cancellationToken)
    {
        var fileName = Path.GetFileName(archivePath);
        LoggingService.LogInformation(
            $"Extracting archive '{fileName}' to '{destinationDirectory}'.");

        try
        {
            if (fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                ZipFile.ExtractToDirectory(archivePath, destinationDirectory, true);
                LoggingService.LogInformation("Zip archive extracted successfully.");
                return;
            }

            if (fileName.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) ||
                fileName.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase))
            {
                await using var fileStream = File.OpenRead(archivePath);
                await using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
                TarFile.ExtractToDirectory(gzipStream, destinationDirectory, true);
                LoggingService.LogInformation("Tar.gz archive extracted successfully.");
                return;
            }

            throw new NotSupportedException($"Unsupported archive format for '{fileName}'.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LoggingService.LogError($"Failed to extract archive '{archivePath}'.", ex);
            throw;
        }
    }

    private static string ResolveContentRoot(string payloadDirectory)
    {
        var directories = Directory
            .GetDirectories(payloadDirectory)
            .Where(dir =>
            {
                var name = Path.GetFileName(dir);
                return !string.Equals(name, "__MACOSX", StringComparison.OrdinalIgnoreCase);
            })
            .ToList();

        var files = Directory.GetFiles(payloadDirectory)
            .Where(file =>
            {
                var name = Path.GetFileName(file);
                return !name.StartsWith(".", StringComparison.Ordinal);
            })
            .ToList();

        if (directories.Count == 1 && files.Count == 0)
            return directories[0];

        return payloadDirectory;
    }

    private static string ResolveExecutableRelativePath(string contentRoot, RuntimePlatform platform,
        string applicationName)
    {
        return platform switch
        {
            RuntimePlatform.Windows => ResolveWindowsExecutable(contentRoot, applicationName),
            RuntimePlatform.MacOS => ResolveMacExecutable(contentRoot, applicationName),
            RuntimePlatform.Linux => ResolveLinuxExecutable(contentRoot, applicationName),
            _ => throw new ArgumentOutOfRangeException(nameof(platform), platform, "Unsupported platform.")
        };
    }

    private static string ResolveWindowsExecutable(string contentRoot, string applicationName)
    {
        var exeName = applicationName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? applicationName
            : $"{applicationName}.exe";

        var candidates = Directory.GetFiles(contentRoot, exeName, SearchOption.AllDirectories);
        if (candidates.Length == 0)
            throw new FileNotFoundException($"Could not find '{exeName}' in extracted payload.");

        var selected = candidates.OrderBy(path => path.Length).First();
        return Path.GetRelativePath(contentRoot, selected);
    }

    private static string ResolveMacExecutable(string contentRoot, string applicationName)
    {
        var bundleName = applicationName.EndsWith(".app", StringComparison.OrdinalIgnoreCase)
            ? applicationName
            : $"{applicationName}.app";

        var bundleCandidates = Directory.GetDirectories(contentRoot, bundleName, SearchOption.AllDirectories);
        if (bundleCandidates.Length > 0)
            return Path.GetRelativePath(contentRoot, bundleCandidates.OrderBy(path => path.Length).First());

        var binaryCandidates = Directory.GetFiles(contentRoot, applicationName, SearchOption.AllDirectories);
        if (binaryCandidates.Length == 0)
            throw new FileNotFoundException(
                $"Could not locate '{applicationName}' bundle or binary in update payload.");

        var selected = binaryCandidates.OrderBy(path => path.Length).First();
        return Path.GetRelativePath(contentRoot, selected);
    }

    private static string ResolveLinuxExecutable(string contentRoot, string applicationName)
    {
        var exactBinary = Directory.GetFiles(contentRoot, applicationName, SearchOption.AllDirectories);
        if (exactBinary.Length > 0)
            return Path.GetRelativePath(contentRoot, exactBinary.OrderBy(path => path.Length).First());

        var appImageCandidates =
            Directory.GetFiles(contentRoot, $"{applicationName}.AppImage", SearchOption.AllDirectories);
        if (appImageCandidates.Length > 0)
            return Path.GetRelativePath(contentRoot, appImageCandidates.OrderBy(path => path.Length).First());

        var shellCandidates = Directory.GetFiles(contentRoot, $"{applicationName}.sh", SearchOption.AllDirectories);
        if (shellCandidates.Length > 0)
            return Path.GetRelativePath(contentRoot, shellCandidates.OrderBy(path => path.Length).First());

        throw new FileNotFoundException($"Could not locate '{applicationName}' executable in update payload.");
    }

    private static ScriptInfo CreateUpdateScript(RuntimePlatform platform, string scriptsDirectory, string contentRoot,
        string installDirectory, string executableRelativePath, Version targetVersion, string applicationName)
    {
        return platform switch
        {
            RuntimePlatform.Windows => CreateWindowsScript(scriptsDirectory, contentRoot, installDirectory,
                executableRelativePath, targetVersion, applicationName),
            RuntimePlatform.MacOS or RuntimePlatform.Linux => CreateUnixScript(scriptsDirectory, contentRoot,
                installDirectory, executableRelativePath, targetVersion, applicationName),
            _ => throw new ArgumentOutOfRangeException(nameof(platform), platform, "Unsupported platform.")
        };
    }

    private static ScriptInfo CreateWindowsScript(string scriptsDirectory, string contentRoot, string installDirectory,
        string executableRelativePath, Version targetVersion, string applicationName)
    {
        var scriptPath = Path.Combine(scriptsDirectory, $"install-{targetVersion}.cmd");
        var builder = new StringBuilder();
        builder.AppendLine("@echo off");
        builder.AppendLine("setlocal");
        builder.AppendLine("set SOURCE=%~1");
        builder.AppendLine("set TARGET=%~2");
        builder.AppendLine("set EXEC_REL=%~3");
        builder.AppendLine("set PID=%~4");
        builder.AppendLine("set APP_NAME=%~5");
        builder.AppendLine($"echo Updating %APP_NAME% to version {targetVersion}...");
        builder.AppendLine(":wait");
        builder.AppendLine("tasklist /FI \"PID eq %PID%\" /FO CSV | findstr /R /C:\",%PID%,\" >NUL");
        builder.AppendLine("if %ERRORLEVEL%==0 (");
        builder.AppendLine("    timeout /t 1 /nobreak >NUL");
        builder.AppendLine("    goto wait");
        builder.AppendLine(")");
        builder.AppendLine("robocopy \"%SOURCE%\" \"%TARGET%\" /MIR /NFL /NDL /NJH /NJS /NC /NS /NP >NUL");
        builder.AppendLine("if errorlevel 8 goto fail");
        builder.AppendLine("set EXEC_PATH=%TARGET%\\%EXEC_REL%");
        builder.AppendLine("if exist \"%EXEC_PATH%\" (");
        builder.AppendLine("    start \"\" \"%EXEC_PATH%\"");
        builder.AppendLine(")");
        builder.AppendLine("exit /b 0");
        builder.AppendLine(":fail");
        builder.AppendLine("echo Update failed while copying files.");
        builder.AppendLine("exit /b 1");

        File.WriteAllText(scriptPath, builder.ToString(), Encoding.UTF8);

        var arguments = new[]
        {
            contentRoot,
            installDirectory,
            executableRelativePath,
            Environment.ProcessId.ToString(),
            applicationName
        };

        return new ScriptInfo(scriptPath, arguments, scriptsDirectory);
    }

    private static ScriptInfo CreateUnixScript(string scriptsDirectory, string contentRoot, string installDirectory,
        string executableRelativePath, Version targetVersion, string applicationName)
    {
        var scriptPath = Path.Combine(scriptsDirectory, $"install-{targetVersion}.sh");
        var builder = new StringBuilder();
        builder.AppendLine("#!/bin/sh");
        builder.AppendLine("SOURCE=\"$1\"");
        builder.AppendLine("TARGET=\"$2\"");
        builder.AppendLine("EXEC_REL=\"$3\"");
        builder.AppendLine("TARGET_PID=\"$4\"");
        builder.AppendLine("APP_NAME=\"$5\"");
        builder.AppendLine($"echo \"Updating $APP_NAME to version {targetVersion}...\"");
        builder.AppendLine("while kill -0 \"$TARGET_PID\" >/dev/null 2>&1; do");
        builder.AppendLine("  sleep 1");
        builder.AppendLine("done");
        builder.AppendLine("mkdir -p \"$TARGET\"");
        builder.AppendLine("if command -v rsync >/dev/null 2>&1; then");
        builder.AppendLine("  rsync -a --delete \"$SOURCE\"/ \"$TARGET\"/");
        builder.AppendLine("else");
        builder.AppendLine("  rm -rf \"$TARGET\"/*");
        builder.AppendLine("  cp -a \"$SOURCE\"/. \"$TARGET\"/");
        builder.AppendLine("fi");
        builder.AppendLine("EXEC_PATH=\"$TARGET/$EXEC_REL\"");
        builder.AppendLine("if [ -d \"$EXEC_PATH\" ] && printf '%s' \"$EXEC_PATH\" | grep -qi '\\.app$'; then");
        builder.AppendLine("  if command -v open >/dev/null 2>&1; then");
        builder.AppendLine("    open \"$EXEC_PATH\" > /dev/null 2>&1 &");
        builder.AppendLine("  else");
        builder.AppendLine("    \"$EXEC_PATH\"/Contents/MacOS/$(basename \"$EXEC_PATH\" .app) > /dev/null 2>&1 &");
        builder.AppendLine("  fi");
        builder.AppendLine("else");
        builder.AppendLine("  chmod +x \"$EXEC_PATH\" >/dev/null 2>&1 || true");
        builder.AppendLine("  \"$EXEC_PATH\" > /dev/null 2>&1 &");
        builder.AppendLine("fi");
        builder.AppendLine("exit 0");

        File.WriteAllText(scriptPath, builder.ToString(), Encoding.UTF8);

        var arguments = new[]
        {
            contentRoot,
            installDirectory,
            executableRelativePath,
            Environment.ProcessId.ToString(),
            applicationName
        };

        return new ScriptInfo(scriptPath, arguments, scriptsDirectory);
    }

    private static void EnsureUnixExecutable(string scriptPath)
    {
        try
        {
            if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
                return;

            const UnixFileMode mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                                      UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                                      UnixFileMode.OtherRead | UnixFileMode.OtherExecute;

            File.SetUnixFileMode(scriptPath, mode);
            LoggingService.LogInformation($"Marked update script '{scriptPath}' as executable.");
        }
        catch (Exception ex)
        {
            LoggingService.LogError($"Failed to set executable bit on '{scriptPath}'.", ex);
        }
    }

    private static string ResolveInstallDirectory()
    {
        var assemblyLocation = AppContext.BaseDirectory;
        if (string.IsNullOrWhiteSpace(assemblyLocation))
            throw new InvalidOperationException("Unable to determine application directory for installation.");

        return Path.GetFullPath(assemblyLocation);
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

    private readonly struct ScriptInfo
    {
        public ScriptInfo(string scriptPath, string[] arguments, string workingDirectory)
        {
            ScriptPath = scriptPath;
            Arguments = arguments;
            WorkingDirectory = workingDirectory;
        }

        public string ScriptPath { get; }
        public string[] Arguments { get; }
        public string WorkingDirectory { get; }
    }
}