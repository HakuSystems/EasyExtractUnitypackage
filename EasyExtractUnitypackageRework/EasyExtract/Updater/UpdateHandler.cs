using System.Diagnostics;
using System.IO;
using System.Net.Http;
using EasyExtract.Config;
using Octokit;
using SharpCompress.Archives;
using SharpCompress.Common;
using FileMode = System.IO.FileMode;

namespace EasyExtract.Updater;

public class UpdateHandler
{
    private readonly ConfigHelper _configHelper = new();
    private readonly BetterLogger _logger = new(); // Added logger initialization

    public async Task<bool> IsUpToDate()
    {
        var latestRelease = await GetLatestReleaseAsync();
        if (latestRelease != null)
        {
            var latestVersion = latestRelease.TagName.TrimStart('v', 'V'); // Removing both 'v' and 'V'
            var currentVersion =
                _configHelper.Config.Update.CurrentVersion.TrimStart('v', 'V'); // Removing both 'v' and 'V'

            await _logger.LogAsync($"Latest version found: {latestVersion}", "UpdateHandler.cs",
                Importance.Info); // Log latest version
            return Version.TryParse(latestVersion, out var latest) &&
                   Version.TryParse(currentVersion, out var current) &&
                   latest <= current;
        }

        await _logger.LogAsync("Failed to fetch the latest release", "UpdateHandler.cs",
            Importance.Warning); // Log fetch failure
        return false;
    }

    public async Task<bool> Update()
    {
        var latestRelease = await GetLatestReleaseAsync();
        if (latestRelease != null)
        {
            var asset = latestRelease.Assets.FirstOrDefault(a => a.Name.EndsWith(".rar"));
            if (asset != null)
            {
                var rarPath = await DownloadAssetAsync(asset.BrowserDownloadUrl);
                var exePath = ExtractRar(rarPath);
                await _logger.LogAsync("Update package downloaded and extracted", "UpdateHandler.cs",
                    Importance.Info); // Log extraction
                Process.Start(new ProcessStartInfo { FileName = await exePath, UseShellExecute = true });
                Environment.Exit(0);
            }
            else
            {
                await _logger.LogAsync("No suitable asset found in the latest release", "UpdateHandler.cs",
                    Importance.Warning); // Log no asset found
                return false;
            }
        }
        else
        {
            await _logger.LogAsync("Failed to fetch the latest release", "UpdateHandler.cs",
                Importance.Warning); // Log fetch failure
            return false;
        }

        return true;
    }

    private async Task<string> DownloadAssetAsync(string url)
    {
        var client = new HttpClient();
        var response = await client.GetAsync(url);
        var fileName = Path.GetFileName(url);

        var currentDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var filePath = Path.Combine(currentDirectory, fileName);

        DeleteOldFiles(currentDirectory);

        await using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await response.Content.CopyToAsync(fs);
        }

        await _logger.LogAsync($"Downloaded asset to: {filePath}", "UpdateHandler.cs", Importance.Info); // Log download
        return filePath;
    }

    private async void DeleteOldFiles(string directory)
    {
        var rarFiles = Directory.GetFiles(directory, "*.rar");
        var extractedFiles = Directory.GetFiles(directory, "*.*").Where(f => !f.EndsWith(".rar")).ToArray();

        foreach (var file in rarFiles) File.Delete(file);
        foreach (var file in extractedFiles) File.Delete(file);

        await _logger.LogAsync("Old files deleted", "UpdateHandler.cs", Importance.Info); // Log file deletion
    }

    private async Task<string?> ExtractRar(string rarPath)
    {
        var currentDirectory = AppDomain.CurrentDomain.BaseDirectory;
        using (var archive = ArchiveFactory.Open(rarPath))
        {
            foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
                entry.WriteToDirectory(currentDirectory,
                    new ExtractionOptions { ExtractFullPath = true, Overwrite = true });
        }

        var exeFile = Directory.GetFiles(currentDirectory, "*.exe").FirstOrDefault();
        await _logger.LogAsync($"RAR extracted, executable found: {exeFile}", "UpdateHandler.cs",
            Importance.Info); // Log extraction
        return exeFile;
    }

    private async Task<Release?> GetLatestReleaseAsync()
    {
        try
        {
            var client = new GitHubClient(new ProductHeaderValue("GitHubUpdater"));
            var releases = await client.Repository.Release.GetAll(_configHelper.Config.Update.RepoOwner,
                _configHelper.Config.Update.RepoName);
            var latestRelease = releases.FirstOrDefault();
            await _logger.LogAsync($"Fetched latest release: {latestRelease?.TagName}", "UpdateHandler.cs",
                Importance.Info); // Log release fetch
            return latestRelease;
        }
        catch (Exception ex)
        {
            await _logger.LogAsync($"Error fetching latest release: {ex.Message}", "UpdateHandler.cs",
                Importance.Error); // Log error
            return null;
        }
    }
}