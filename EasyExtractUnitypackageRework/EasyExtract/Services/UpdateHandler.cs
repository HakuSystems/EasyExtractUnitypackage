using EasyExtract.Config;
using EasyExtract.Models;
using EasyExtract.Utilities;

namespace EasyExtract.Services;

public class UpdateHandler
{
    private readonly ConfigHelper _configHelper = new();

    public async Task<bool> IsUpToDateOrUpdate(bool updateIfNeeded)
    {
        var latestRelease = await GetLatestReleaseAsync();
        if (latestRelease == null)
        {
            await BetterLogger.LogAsync("Failed to fetch the latest release", Importance.Warning);
            return false;
        }

        var latestVersion = latestRelease.TagName.TrimStart('v', 'V');
        var currentVersion = GetCurrentAssemblyVersion();

        await BetterLogger.LogAsync($"Latest version found: {latestVersion}", Importance.Info);
        await BetterLogger.LogAsync($"Current version: {currentVersion}", Importance.Info);

        if (Version.TryParse(latestVersion, out var latest) && Version.TryParse(currentVersion, out var current))
        {
            await BetterLogger.LogAsync($"Parsed latest version: {latest}", Importance.Info);
            await BetterLogger.LogAsync($"Parsed current version: {current}", Importance.Info);

            var isUpToDate = latest <= current;
            await BetterLogger.LogAsync($"Is up to date: {isUpToDate}", Importance.Info);

            if (isUpToDate || !updateIfNeeded) return isUpToDate;

            var asset = latestRelease.Assets.FirstOrDefault(a => a.Name.EndsWith(".rar"));
            if (asset == null)
            {
                await BetterLogger.LogAsync("No suitable asset found in the latest release",
                    Importance.Warning);
                return false;
            }

            var rarPath = await DownloadAssetAsync(asset.BrowserDownloadUrl, latestRelease.TagName);
            var exePath = await ExtractRarAsync(rarPath);

            if (string.IsNullOrEmpty(exePath))
            {
                await BetterLogger.LogAsync("Executable not found after extraction",
                    Importance.Warning);
                return false;
            }

            await BetterLogger.LogAsync("Update package downloaded and extracted", Importance.Info);
            return await TryRunNewExecutable(latestRelease, exePath);
        }

        await BetterLogger.LogAsync("Failed to parse version strings", Importance.Error);
        return false;
    }

    private async Task<bool> TryRunNewExecutable(Release latestRelease, string exePath)
    {
        try
        {
            var exeFileName = Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().Location);
            var originalDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (originalDirectory != null)
            {
                var destinationPath = Path.Combine(originalDirectory, $"{exeFileName}_{latestRelease.TagName}.exe");
                File.Copy(exePath, destinationPath, true);

                Process.Start(new ProcessStartInfo
                {
                    FileName = destinationPath,
                    UseShellExecute = true,
                    WorkingDirectory = originalDirectory
                });

                Environment.Exit(0);
            }
        }
        catch (Exception ex)
        {
            await BetterLogger.LogAsync($"Error starting new process: {ex.Message}", Importance.Error);
            return false;
        }

        return true;
    }

    private static async Task<string> DownloadAssetAsync(string url, string versionTag)
    {
        using var client = new HttpClient();
        var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var fileName = $"{Path.GetFileNameWithoutExtension(url)}_{versionTag}{Path.GetExtension(url)}";

        // Download to a temporary directory
        var tempDirectory = Path.Combine(Path.GetTempPath(), "EasyExtractUpdate");
        Directory.CreateDirectory(tempDirectory);
        var filePath = Path.Combine(tempDirectory, fileName);

        await DeleteOldFilesAsync(tempDirectory);

        await using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await response.Content.CopyToAsync(fs);
        }

        await BetterLogger.LogAsync($"Downloaded asset to: {filePath}", Importance.Info);
        return filePath;
    }

    private static async Task DeleteOldFilesAsync(string directory)
    {
        var rarFiles = Directory.GetFiles(directory, "*.rar");
        var extractedFiles = Directory.GetFiles(directory, "*.*").Where(f => !f.EndsWith(".rar")).ToArray();

        foreach (var file in rarFiles) File.Delete(file);
        foreach (var file in extractedFiles) File.Delete(file);

        await BetterLogger.LogAsync("Old files deleted", Importance.Info);
    }

    private async Task<string?> ExtractRarAsync(string rarPath)
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "EasyExtractUpdate");
        try
        {
            using (var archive = ArchiveFactory.Open(rarPath))
            {
                var entries = archive.Entries.Where(entry => !entry.IsDirectory).ToList();
                if (!entries.Any())
                {
                    await BetterLogger.LogAsync($"No files found in RAR archive: {rarPath}",
                        Importance.Warning);
                    return null;
                }

                foreach (var entry in entries)
                {
                    await BetterLogger.LogAsync($"Extracting entry: {entry.Key}", Importance.Info);
                    entry.WriteToDirectory(tempDirectory,
                        new ExtractionOptions
                        {
                            ExtractFullPath = true,
                            Overwrite = true
                        });
                }
            }

            // Log the contents of the extraction directory
            var extractedFiles = Directory.GetFiles(tempDirectory, "*.*", SearchOption.AllDirectories);
            foreach (var file in extractedFiles)
                await BetterLogger.LogAsync($"Extracted file: {file}", Importance.Info);

            // Search for the executable in all subdirectories
            var exeFile = Directory.GetFiles(tempDirectory, "*.exe", SearchOption.AllDirectories).FirstOrDefault();
            await BetterLogger.LogAsync($"RAR extracted, executable found: {exeFile}",
                Importance.Info);
            return exeFile;
        }
        catch (UnauthorizedAccessException ex)
        {
            await BetterLogger.LogAsync($"Access to the path is denied: {ex.Message}",
                Importance.Error);
            return null;
        }
        catch (Exception ex)
        {
            await BetterLogger.LogAsync($"Error extracting RAR: {ex.Message}", Importance.Error);
            return null;
        }
    }

    private async Task<Release?> GetLatestReleaseAsync()
    {
        try
        {
            var client = new GitHubClient(new ProductHeaderValue("GitHubUpdater"));
            var releases = await client.Repository.Release.GetAll(_configHelper.Config.Update.RepoOwner,
                _configHelper.Config.Update.RepoName);
            var latestRelease = releases.FirstOrDefault();
            if (latestRelease.ToString().Contains("API rate limit exceeded"))
            {
                await BetterLogger.LogAsync("API rate limit exceeded", Importance.Warning);
                return null;
            }

            await BetterLogger.LogAsync($"Fetched latest release: {latestRelease?.TagName}",
                Importance.Info);
            return latestRelease;
        }
        catch (Exception ex)
        {
            await BetterLogger.LogAsync($"Error fetching latest release: {ex.Message}",
                Importance.Error);
            return null;
        }
    }

    private static string GetCurrentAssemblyVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        return version != null ? version.ToString() : "0.0.0";
    }
}