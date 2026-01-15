using Velopack;
using Velopack.Sources;

namespace EasyExtractCrossPlatform.Services;

public interface IVelopackUpdateService
{
    Task<UpdateInfo?> CheckForUpdatesAsync(bool isPrerelease = false);
    Task DownloadUpdatesAsync(UpdateInfo update, Action<int> progress);
    void ApplyUpdates(UpdateInfo update);
}

public class VelopackUpdateService : IVelopackUpdateService
{
    private const string RepoUrl = "https://github.com/HakuSystems/EasyExtractUnitypackage";

    public async Task<UpdateInfo?> CheckForUpdatesAsync(bool isPrerelease = false)
    {
        try
        {
            var mgr = new UpdateManager(new GithubSource(RepoUrl, null, isPrerelease));
            var newVersion = await mgr.CheckForUpdatesAsync();
            return newVersion;
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Failed to check for updates", ex);
            return null;
        }
    }

    public async Task DownloadUpdatesAsync(UpdateInfo update, Action<int> progress)
    {
        try
        {
            var mgr = new UpdateManager(new GithubSource(RepoUrl, null, false));
            await mgr.DownloadUpdatesAsync(update, progress);
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
            var mgr = new UpdateManager(new GithubSource(RepoUrl, null, false));
            mgr.ApplyUpdatesAndRestart(update);
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Failed to apply updates", ex);
            throw;
        }
    }
}