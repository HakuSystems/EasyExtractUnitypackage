using EasyExtract.Config;
using EasyExtract.Utilities;

namespace EasyExtract.Updater;

public class UpdateHandler
{
    private readonly ConfigHelper _configHelper = new();
    private readonly BetterLogger _logger = new();

    public async Task<bool> IsUpToDate()
    {
        var latestRelease = await GetLatestReleaseAsync();
        if (latestRelease != null)
        {
            var latestVersion = latestRelease.TagName.TrimStart('v', 'V');
            var currentVersion = GetCurrentAssemblyVersion();

            await _logger.LogAsync($"Latest version found: {latestVersion}", "UpdateHandler.cs", Importance.Info);
            await _logger.LogAsync($"Current version: {currentVersion}", "UpdateHandler.cs", Importance.Info);

            if (Version.TryParse(latestVersion, out var latest) && Version.TryParse(currentVersion, out var current))
            {
                await _logger.LogAsync($"Parsed latest version: {latest}", "UpdateHandler.cs", Importance.Info);
                await _logger.LogAsync($"Parsed current version: {current}", "UpdateHandler.cs", Importance.Info);

                var isUpToDate = latest <= current;
                await _logger.LogAsync($"Is up to date: {isUpToDate}", "UpdateHandler.cs", Importance.Info);

                return isUpToDate;
            }

            await _logger.LogAsync("Failed to parse version strings", "UpdateHandler.cs", Importance.Error);
        }

        await _logger.LogAsync("Failed to fetch the latest release", "UpdateHandler.cs", Importance.Warning);
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
                var rarPath = await DownloadAssetAsync(asset.BrowserDownloadUrl, latestRelease.TagName);
                var exePath = await ExtractRarAsync(rarPath);

                if (!string.IsNullOrEmpty(exePath))
                {
                    await _logger.LogAsync("Update package downloaded and extracted", "UpdateHandler.cs",
                        Importance.Info);

                    try
                    {
                        // Run the new executable from the original location
                        var appname = Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().Location);
                        var originalDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                        var destinationPath = Path.Combine(originalDirectory, $"{appname}_{latestRelease.TagName}.exe");
                        File.Copy(exePath, destinationPath, true);

                        Process.Start(new ProcessStartInfo
                        {
                            FileName = destinationPath,
                            UseShellExecute = true,
                            WorkingDirectory = originalDirectory
                        });
                        Environment.Exit(0);
                    }
                    catch (Exception ex)
                    {
                        await _logger.LogAsync($"Error starting new process: {ex.Message}", "UpdateHandler.cs",
                            Importance.Error);
                        return false;
                    }
                }
                else
                {
                    await _logger.LogAsync("Executable not found after extraction", "UpdateHandler.cs",
                        Importance.Warning);
                    return false;
                }
            }
            else
            {
                await _logger.LogAsync("No suitable asset found in the latest release", "UpdateHandler.cs",
                    Importance.Warning);
                return false;
            }
        }
        else
        {
            await _logger.LogAsync("Failed to fetch the latest release", "UpdateHandler.cs", Importance.Warning);
            return false;
        }

        return true;
    }

    private async Task<string> DownloadAssetAsync(string url, string versionTag)
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

        await _logger.LogAsync($"Downloaded asset to: {filePath}", "UpdateHandler.cs", Importance.Info);
        return filePath;
    }

    private async Task DeleteOldFilesAsync(string directory)
    {
        var rarFiles = Directory.GetFiles(directory, "*.rar");
        var extractedFiles = Directory.GetFiles(directory, "*.*").Where(f => !f.EndsWith(".rar")).ToArray();

        foreach (var file in rarFiles) File.Delete(file);
        foreach (var file in extractedFiles) File.Delete(file);

        await _logger.LogAsync("Old files deleted", "UpdateHandler.cs", Importance.Info);
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
                    await _logger.LogAsync($"No files found in RAR archive: {rarPath}", "UpdateHandler.cs",
                        Importance.Warning);
                    return null;
                }

                foreach (var entry in entries)
                {
                    await _logger.LogAsync($"Extracting entry: {entry.Key}", "UpdateHandler.cs", Importance.Info);
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
                await _logger.LogAsync($"Extracted file: {file}", "UpdateHandler.cs", Importance.Info);

            // Search for the executable in all subdirectories
            var exeFile = Directory.GetFiles(tempDirectory, "*.exe", SearchOption.AllDirectories).FirstOrDefault();
            await _logger.LogAsync($"RAR extracted, executable found: {exeFile}", "UpdateHandler.cs", Importance.Info);
            return exeFile;
        }
        catch (UnauthorizedAccessException ex)
        {
            await _logger.LogAsync($"Access to the path is denied: {ex.Message}", "UpdateHandler.cs", Importance.Error);
            return null;
        }
        catch (Exception ex)
        {
            await _logger.LogAsync($"Error extracting RAR: {ex.Message}", "UpdateHandler.cs", Importance.Error);
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
            await _logger.LogAsync($"Fetched latest release: {latestRelease?.TagName}", "UpdateHandler.cs",
                Importance.Info);
            return latestRelease;
        }
        catch (Exception ex)
        {
            await _logger.LogAsync($"Error fetching latest release: {ex.Message}", "UpdateHandler.cs",
                Importance.Error);
            return null;
        }
    }

    private string GetCurrentAssemblyVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        return version != null ? version.ToString() : "0.0.0";
    }
}