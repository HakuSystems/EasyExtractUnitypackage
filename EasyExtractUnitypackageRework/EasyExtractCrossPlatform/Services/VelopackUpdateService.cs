using System.Net;
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

public enum UpdateCheckMode
{
    Interactive,
    SilentBackground
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
    Task<UpdateCheckResult> CheckForUpdatesAsync(UpdateCheckMode mode = UpdateCheckMode.Interactive,
        bool isPrerelease = false);

    Task DownloadUpdatesAsync(UpdateInfo update, Action<int> progress);
    void ApplyUpdates(UpdateInfo update);
}

internal interface IVelopackUpdateManager
{
    bool IsInstalled { get; }
    Task<UpdateInfo?> CheckForUpdatesAsync();
    Task DownloadUpdatesAsync(UpdateInfo update, Action<int> progress);
    void ApplyUpdatesAndRestart(UpdateInfo update);
}

public class VelopackUpdateService : IVelopackUpdateService
{
    private const string RepoUrl = "https://github.com/HakuSystems/EasyExtractUnitypackage";
    private const string MissingLocatorMessage = "No VelopackLocator has been set";
    private readonly bool _requireLocator;
    private readonly Func<bool, IVelopackUpdateManager?> _updateManagerFactory;

    public VelopackUpdateService() : this(CreateUpdateManager, true)
    {
    }

    internal VelopackUpdateService(Func<bool, IVelopackUpdateManager?> updateManagerFactory) : this(
        updateManagerFactory,
        false)
    {
    }

    private VelopackUpdateService(Func<bool, IVelopackUpdateManager?> updateManagerFactory, bool requireLocator)
    {
        _updateManagerFactory = updateManagerFactory ?? throw new ArgumentNullException(nameof(updateManagerFactory));
        _requireLocator = requireLocator;
    }

    public async Task<UpdateCheckResult> CheckForUpdatesAsync(UpdateCheckMode mode = UpdateCheckMode.Interactive,
        bool isPrerelease = false)
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
        catch (HttpRequestException ex) when (mode == UpdateCheckMode.SilentBackground && IsGitHubRateLimit(ex))
        {
            LoggingService.LogInformation(
                $"Skipping silent update check because GitHub rate limiting is active: {ex.Message}");
            return UpdateCheckResult.Unavailable;
        }
        catch (NotInstalledException)
        {
            LoggingService.LogInformation(
                "Application is not installed (likely dev environment). Skipping update check.");
            return UpdateCheckResult.Unavailable;
        }
        catch (Exception ex) when (mode == UpdateCheckMode.SilentBackground)
        {
            LoggingService.LogWarning("Silent update check failed.", ex);
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

    private bool TryCreateUpdateManager(bool isPrerelease, out IVelopackUpdateManager? manager)
    {
        manager = null;

        if (_requireLocator && !VelopackLocator.IsCurrentSet)
        {
            LoggingService.LogInformation("Skipping update operation: Velopack locator is not initialized.");
            return false;
        }

        try
        {
            manager = _updateManagerFactory(isPrerelease);
            return true;
        }
        catch (InvalidOperationException ex) when
            (ex.Message.Contains(MissingLocatorMessage, StringComparison.Ordinal))
        {
            LoggingService.LogInformation("Skipping update operation: Velopack locator is not initialized.");
            return false;
        }
    }

    private static IVelopackUpdateManager CreateUpdateManager(bool isPrerelease)
    {
        return new VelopackUpdateManagerAdapter(new UpdateManager(new GithubSource(RepoUrl, null, isPrerelease)));
    }

    private static bool IsGitHubRateLimit(HttpRequestException ex)
    {
        if (ex.StatusCode != HttpStatusCode.Forbidden)
            return false;

        return ex.Message.Contains("rate limit", StringComparison.OrdinalIgnoreCase);
    }

    private sealed class VelopackUpdateManagerAdapter : IVelopackUpdateManager
    {
        private readonly UpdateManager _inner;

        public VelopackUpdateManagerAdapter(UpdateManager inner)
        {
            _inner = inner;
        }

        public bool IsInstalled => _inner.IsInstalled;

        public Task<UpdateInfo?> CheckForUpdatesAsync()
        {
            return _inner.CheckForUpdatesAsync();
        }

        public Task DownloadUpdatesAsync(UpdateInfo update, Action<int> progress)
        {
            return _inner.DownloadUpdatesAsync(update, progress);
        }

        public void ApplyUpdatesAndRestart(UpdateInfo update)
        {
            _inner.ApplyUpdatesAndRestart(update);
        }
    }
}