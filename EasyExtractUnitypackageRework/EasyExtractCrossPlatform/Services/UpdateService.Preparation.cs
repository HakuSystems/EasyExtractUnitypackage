using System;
using System.IO;
using System.Threading;

namespace EasyExtractCrossPlatform.Services;

public sealed partial class UpdateService
{
    public async Task<UpdateInstallResult> DownloadAndPrepareUpdateAsync(UpdateManifest manifest,
        IProgress<UpdateProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        using var prepareUpdateScope = LoggingService.BeginPerformanceScope("PrepareUpdate", "Updater",
            manifest.Version.ToString());

        try
        {
            LoggingService.LogInformation(
                $"PrepareUpdate: start | version={manifest.Version} | asset={manifest.Asset.Name} | uri={manifest.Asset.DownloadUri}");
            progress?.Report(new UpdateProgress(UpdatePhase.Downloading, 0.0));

            var updatesRoot = Path.Combine(AppSettingsService.SettingsDirectory, "Updates");
            Directory.CreateDirectory(updatesRoot);
            LoggingService.LogInformation($"PrepareUpdate: using updates root | path='{updatesRoot}'");

            var versionDirectory = Path.Combine(updatesRoot, manifest.Version.ToString());
            if (Directory.Exists(versionDirectory))
            {
                LoggingService.LogInformation(
                    $"PrepareUpdate: clearing existing stage directory | path='{versionDirectory}'");
                Directory.Delete(versionDirectory, true);
            }

            Directory.CreateDirectory(versionDirectory);
            LoggingService.LogInformation($"PrepareUpdate: working directory ready | path='{versionDirectory}'");

            var archivePath = Path.Combine(versionDirectory, manifest.Asset.Name);
            await DownloadAssetAsync(manifest.Asset, archivePath, progress, cancellationToken).ConfigureAwait(false);
            LoggingService.LogInformation($"PrepareUpdate: asset downloaded | path='{archivePath}'");

            cancellationToken.ThrowIfCancellationRequested();

            progress?.Report(new UpdateProgress(UpdatePhase.Extracting, null));

            var payloadDirectory = Path.Combine(versionDirectory, "payload");
            Directory.CreateDirectory(payloadDirectory);
            await ExtractArchiveAsync(archivePath, payloadDirectory, cancellationToken).ConfigureAwait(false);
            LoggingService.LogInformation($"PrepareUpdate: payload extracted | path='{payloadDirectory}'");

            cancellationToken.ThrowIfCancellationRequested();

            progress?.Report(new UpdateProgress(UpdatePhase.Preparing, null));

            var contentRoot = ResolveContentRoot(payloadDirectory);
            var platform = OperatingSystemInfo.GetCurrentPlatform();
            var appName = OperatingSystemInfo.GetApplicationName();
            var executableRelativePath = ResolveExecutableRelativePath(contentRoot, platform, appName);
            var installDirectory = ResolveInstallDirectory();
            LoggingService.LogInformation(
                $"PrepareUpdate: resolved directories | contentRoot='{contentRoot}' | installDirectory='{installDirectory}'");

            var scriptsDirectory = Path.Combine(versionDirectory, "scripts");
            Directory.CreateDirectory(scriptsDirectory);

            var scriptInfo = CreateUpdateScript(platform, scriptsDirectory, contentRoot, installDirectory,
                executableRelativePath, manifest.Version, appName);

            if (!OperatingSystem.IsWindows())
                EnsureUnixExecutable(scriptInfo.ScriptPath);

            progress?.Report(new UpdateProgress(UpdatePhase.Preparing, 1.0));
            LoggingService.LogInformation(
                $"PrepareUpdate: script ready | platform={platform} | scriptPath='{scriptInfo.ScriptPath}' | workingDirectory='{scriptInfo.WorkingDirectory}'");

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
                $"PrepareUpdate: complete | stageDirectory='{preparation.StageDirectory}' | version={preparation.TargetVersion}");
            return UpdateInstallResult.FromPreparation(preparation);
        }
        catch (OperationCanceledException)
        {
            LoggingService.LogInformation("PrepareUpdate: cancelled");
            return UpdateInstallResult.Failed("Update preparation was cancelled.");
        }
        catch (Exception ex)
        {
            LoggingService.LogError("PrepareUpdate: failure", ex);
            return UpdateInstallResult.Failed("Failed to prepare update.", ex);
        }
    }
}
