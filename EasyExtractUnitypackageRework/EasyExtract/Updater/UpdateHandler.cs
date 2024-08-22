using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using EasyExtract.Config;
using Octokit;
using SharpCompress.Archives;
using SharpCompress.Common;
using FileMode = System.IO.FileMode;

namespace EasyExtract.Updater;

public class UpdateHandler
{
    private readonly ConfigHelper _configHelper = new();
    private readonly BetterLogger _logger = new();

    /// <summary>
    ///     Checks if the application is up to date by comparing the current version with the latest version available.
    /// </summary>
    /// <returns>
    ///     <c>true</c> if the application is up to date; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    ///     This method uses the <see cref="GetLatestReleaseAsync" /> method to fetch the latest release version.
    ///     It then compares the latest version with the current version of the application.
    ///     If the latest version is parsed successfully and is less than or equal to the current version, the application is
    ///     considered up to date.
    ///     Otherwise, the method returns <c>false</c>.
    /// </remarks>
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

    /// <summary>
    ///     Handles the update process of the application.
    /// </summary>
    /// <returns>
    ///     A task representing the asynchronous operation. The task result will be true if the update was successful,
    ///     false otherwise.
    /// </returns>
    public async Task<bool> Update()
    {
        var latestRelease = await GetLatestReleaseAsync();
        if (latestRelease != null)
        {
            var asset = latestRelease.Assets.FirstOrDefault(a => a.Name.EndsWith(".rar"));
            if (asset != null)
            {
                var rarPath = await DownloadAssetAsync(asset.BrowserDownloadUrl);
                var exePath = await ExtractRarAsync(rarPath);

                if (!string.IsNullOrEmpty(exePath))
                {
                    await _logger.LogAsync("Update package downloaded and extracted", "UpdateHandler.cs",
                        Importance.Info);

                    try
                    {
                        // Run the new executable from a temporary location
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = exePath,
                            UseShellExecute = true,
                            WorkingDirectory = Path.GetDirectoryName(exePath)
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

    /// <summary>
    ///     Asynchronously downloads an asset from a specified URL.
    /// </summary>
    /// <param name="url">The URL of the asset to download.</param>
    /// <returns>The path to the downloaded file.</returns>
    private async Task<string> DownloadAssetAsync(string url)
    {
        using var client = new HttpClient();
        var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var fileName = Path.GetFileName(url);

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

    /// <summary>
    ///     Deletes old files from the specified directory.
    /// </summary>
    /// <param name="directory">The directory from which to delete old files.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task DeleteOldFilesAsync(string directory)
    {
        var rarFiles = Directory.GetFiles(directory, "*.rar");
        var extractedFiles = Directory.GetFiles(directory, "*.*").Where(f => !f.EndsWith(".rar")).ToArray();

        foreach (var file in rarFiles) File.Delete(file);
        foreach (var file in extractedFiles) File.Delete(file);

        await _logger.LogAsync("Old files deleted", "UpdateHandler.cs", Importance.Info);
    }

    /// <summary>
    ///     Extracts files from a RAR archive asynchronously.
    /// </summary>
    /// <param name="rarPath">The path to the RAR archive.</param>
    /// <returns>
    ///     The path to the extracted executable file if successful, or null if no executable file is found or an error occurs
    ///     during extraction.
    /// </returns>
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
                        new ExtractionOptions { ExtractFullPath = true, Overwrite = true });
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


    /// <summary>
    ///     Asynchronously retrieves the latest release from a GitHub repository.
    /// </summary>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result is the latest <see cref="Release" /> object from
    ///     the repository.
    /// </returns>
    /// <remarks>
    ///     This method fetches the latest release from a GitHub repository specified in the configuration. It uses the GitHub
    ///     API to
    ///     retrieve the releases and returns the latest release. If an error occurs during the retrieval process,
    ///     <see langword="null" /> is returned.
    /// </remarks>
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

    /// <summary>
    ///     Returns the current version of the executing assembly.
    /// </summary>
    /// <returns>
    ///     The current version of the executing assembly as a string. Returns "0.0.0" if the version cannot be
    ///     determined.
    /// </returns>
    private string GetCurrentAssemblyVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        return version != null ? version.ToString() : "0.0.0";
    }
}