using Velopack;
using Velopack.Exceptions;
using Velopack.Locators;
using Velopack.Sources;

namespace EasyExtractCrossPlatform.Services;

public enum UpdateCheckState
{
    UpToDate,
    UpdateAvailable,
    Unavailable
}

public sealed record UpdateCheckResult(UpdateCheckState State, UpdateInfo? UpdateInfo = null)
{
    public static UpdateCheckResult UpToDate { get; } = new(UpdateCheckState.UpToDate);
    public static UpdateCheckResult Unavailable { get; } = new(UpdateCheckState.Unavailable);

    public static UpdateCheckResult Available(UpdateInfo updateInfo)
    {
        ArgumentNullException.ThrowIfNull(updateInfo);
        return new UpdateCheckResult(UpdateCheckState.UpdateAvailable, updateInfo);
    }
}

public interface IVelopackUpdateService
{
    Task<UpdateCheckResult> CheckForUpdatesAsync(bool isPrerelease = false);
    Task DownloadUpdatesAsync(UpdateInfo update, Action<int> progress);
    void ApplyUpdates(UpdateInfo update);
}

public class VelopackUpdateService : IVelopackUpdateService
{
    private const string RepoUrl = "https://github.com/HakuSystems/EasyExtractUnitypackage";
    private const string MissingLocatorMessage = "No VelopackLocator has been set";

    public async Task<UpdateCheckResult> CheckForUpdatesAsync(bool isPrerelease = false)
    {
        try
        {
            if (!TryCreateUpdateManager(isPrerelease, out var mgr) || mgr is null)
                return UpdateCheckResult.Unavailable;

            if (!mgr.IsInstalled)
            {
                LoggingService.LogInformation(
                    "Application is not installed (likely dev environment). Skipping update check.");
                return UpdateCheckResult.Unavailable;
            }

            var newVersion = await mgr.CheckForUpdatesAsync();
            return newVersion is null
                ? UpdateCheckResult.UpToDate
                : UpdateCheckResult.Available(newVersion);
        }
        catch (NotInstalledException)
        {
            LoggingService.LogInformation(
                "Application is not installed (likely dev environment). Skipping update check.");
            return UpdateCheckResult.Unavailable;
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Failed to check for updates", ex);
            throw;
        }
    }

    public async Task DownloadUpdatesAsync(UpdateInfo update, Action<int> progress)
    {
        try
        {
            if (!TryCreateUpdateManager(false, out var mgr) || mgr is null)
                return;

            if (!mgr.IsInstalled)
            {
                LoggingService.LogWarning("Cannot download updates: Application is not installed.");
                return;
            }

            await mgr.DownloadUpdatesAsync(update, progress);
        }
        catch (NotInstalledException ex)
        {
            LoggingService.LogWarning("Cannot download updates: Application is not installed.", ex);
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Failed to download updates", ex);
            throw;
        }
    }

    public void ApplyUpdates(UpdateInfo update)
    {
        try
        {
            if (!TryCreateUpdateManager(false, out var mgr) || mgr is null)
                return;

            if (!mgr.IsInstalled)
            {
                LoggingService.LogWarning("Cannot apply updates: Application is not installed.");
                return;
            }

            mgr.ApplyUpdatesAndRestart(update);
        }
        catch (NotInstalledException ex)
        {
            LoggingService.LogWarning("Cannot apply updates: Application is not installed.", ex);
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Failed to apply updates", ex);
            throw;
        }
    }

    private static bool TryCreateUpdateManager(bool isPrerelease, out UpdateManager? manager)
    {
        manager = null;

        if (!VelopackLocator.IsCurrentSet)
        {
            LoggingService.LogInformation("Skipping update operation: Velopack locator is not initialized.");
            return false;
        }

        try
        {
            manager = new UpdateManager(new GithubSource(RepoUrl, null, isPrerelease));
            return true;
        }
        catch (InvalidOperationException ex) when
            (ex.Message.Contains(MissingLocatorMessage, StringComparison.Ordinal))
        {
            LoggingService.LogInformation("Skipping update operation: Velopack locator is not initialized.");
            return false;
        }
    }
}