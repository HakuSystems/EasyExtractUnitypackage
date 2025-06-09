using EasyExtract.Config;
using EasyExtract.Utilities.Logger;
using Octokit;

namespace EasyExtract.Services;

public class UpdateHandler
{
    public async Task<bool> IsUpToDateOrUpdate(bool updateIfNeeded)
    {
        var latestRelease = await GetLatestReleaseAsync();
        if (latestRelease == null)
        {
            BetterLogger.Info("Failed to fetch the latest release from GitHub. Update check aborted.",
                "Updates");
            return false;
        }

        var latestVersion = latestRelease.TagName.TrimStart('v', 'V');
        var currentVersion = GetCurrentAssemblyVersion();

        BetterLogger.Info($"Latest version found: {latestVersion}", "Updates");
        BetterLogger.Info($"Current version: {currentVersion}", "Updates");

        if (Version.TryParse(latestVersion, out var latest) && Version.TryParse(currentVersion, out var current))
        {
            BetterLogger.Debug($"Parsed latest version: {latest}", "Updates");
            BetterLogger.Debug($"Parsed current version: {current}", "Updates");

            var isUpToDate = latest <= current;
            BetterLogger.Info($"Is up to date: {isUpToDate}", "Updates");

            if (isUpToDate || !updateIfNeeded) return isUpToDate;

            var asset = latestRelease.Assets.FirstOrDefault(a => a.Name.EndsWith(".rar"));
            if (asset == null)
            {
                BetterLogger.Warning("No suitable asset found in the latest release", "Updates");
                return false;
            }

            var rarPath = await DownloadAssetAsync(asset.BrowserDownloadUrl, latestRelease.TagName);
            var exePath = await ExtractRarAsync(rarPath);

            if (string.IsNullOrEmpty(exePath))
            {
                BetterLogger.Warning("Executable not found after extraction", "Updates");
                return false;
            }

            BetterLogger.Info("Update package downloaded and extracted", "Updates");
            return await TryRunNewExecutable(latestRelease, exePath);
        }

        BetterLogger.Error("Failed to parse version strings", "Updates");
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
            BetterLogger.Error($"Error starting new process: {ex.Message}", "Updates");
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

        BetterLogger.Info($"Downloaded asset to: {filePath}", "Updates");
        return filePath;
    }

    private static async Task DeleteOldFilesAsync(string directory)
    {
        var rarFiles = Directory.GetFiles(directory, "*.rar");
        var extractedFiles = Directory.GetFiles(directory, "*.*").Where(f => !f.EndsWith(".rar")).ToArray();

        foreach (var file in rarFiles) File.Delete(file);
        foreach (var file in extractedFiles) File.Delete(file);

        BetterLogger.Info("Old files deleted", "Updates");
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
                    BetterLogger.Warning($"No files found in RAR archive: {rarPath}", "Updates");
                    return null;
                }

                foreach (var entry in entries)
                {
                    BetterLogger.Info($"Extracting entry: {entry.Key}", "Updates");
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
                BetterLogger.Info($"Extracted file: {file}", "Updates");

            // Search for the executable in all subdirectories
            var exeFile = Directory.GetFiles(tempDirectory, "*.exe", SearchOption.AllDirectories).FirstOrDefault();
            BetterLogger.Info($"RAR extracted, executable found: {exeFile}", "Updates");
            return exeFile;
        }
        catch (UnauthorizedAccessException ex)
        {
            BetterLogger.Error($"Access to the path is denied: {ex.Message}", "Updates");
            return null;
        }
        catch (Exception ex)
        {
            BetterLogger.Error($"Error extracting RAR: {ex.Message}", "Updates");
            return null;
        }
    }

    private static async Task<Release?> GetLatestReleaseAsync()
    {
        try
        {
            var client = new GitHubClient(new ProductHeaderValue("GitHubUpdater"));
            var releases = await client.Repository.Release.GetAll(ConfigHandler.Instance.Config.Update.RepoOwner,
                ConfigHandler.Instance.Config.Update.RepoName);
            var latestRelease = releases.FirstOrDefault();
            if (latestRelease!.ToString()!.Contains("API rate limit exceeded"))
            {
                BetterLogger.Warning("API rate limit exceeded", "Updates");
                return null;
            }

            BetterLogger.Info($"Fetched latest release: {latestRelease.TagName}", "Updates");
            return latestRelease;
        }
        catch (Exception ex)
        {
            BetterLogger.Error($"Error fetching latest release: {ex.Message}", "Updates");
            return null;
        }
    }

    private static string GetCurrentAssemblyVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        return version != null ? version.ToString() : "0.0.0";
    }
}