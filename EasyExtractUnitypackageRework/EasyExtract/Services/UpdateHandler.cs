using EasyExtract.Config;
using EasyExtract.Models;
using EasyExtract.Utilities;

namespace EasyExtract.Services;

public class UpdateHandler
{
    private readonly ConfigHelper _configHelper = new();

    public async Task<bool> IsUpToDate()
    {
        var latestRelease = await GetLatestReleaseAsync();
        if (latestRelease != null)
        {
            var latestVersion = latestRelease.TagName.TrimStart('v', 'V');
            var currentVersion = GetCurrentAssemblyVersion();

            await BetterLogger.LogAsync($"Latest version found: {latestVersion}", $"{nameof(UpdateHandler)}.cs", Importance.Info);
            await BetterLogger.LogAsync($"Current version: {currentVersion}", $"{nameof(UpdateHandler)}.cs", Importance.Info);

            if (Version.TryParse(latestVersion, out var latest) && Version.TryParse(currentVersion, out var current))
            {
                await BetterLogger.LogAsync($"Parsed latest version: {latest}", $"{nameof(UpdateHandler)}.cs", Importance.Info);
                await BetterLogger.LogAsync($"Parsed current version: {current}", $"{nameof(UpdateHandler)}.cs", Importance.Info);

                var isUpToDate = latest <= current;
                await BetterLogger.LogAsync($"Is up to date: {isUpToDate}", $"{nameof(UpdateHandler)}.cs", Importance.Info);

                return isUpToDate;
            }

            await BetterLogger.LogAsync("Failed to parse version strings", $"{nameof(UpdateHandler)}.cs", Importance.Error);
        }

        await BetterLogger.LogAsync("Failed to fetch the latest release", $"{nameof(UpdateHandler)}.cs", Importance.Warning);
        return false;
    }

    public async Task<bool> Update()
    {
        var latestRelease = await GetLatestReleaseAsync();
        if (latestRelease == null)
        {
            await BetterLogger.LogAsync("Failed to fetch the latest release", $"{nameof(UpdateHandler)}.cs", Importance.Warning);
            return false;
        }

        var asset = latestRelease.Assets.FirstOrDefault(a => a.Name.EndsWith(".rar"));
        if (asset == null)
        {
            await BetterLogger.LogAsync("No suitable asset found in the latest release", $"{nameof(UpdateHandler)}.cs",
                Importance.Warning);
            return false;
        }

        var rarPath = await DownloadAssetAsync(asset.BrowserDownloadUrl, latestRelease.TagName);
        var exePath = await ExtractRarAsync(rarPath);

        if (string.IsNullOrEmpty(exePath))
        {
            await BetterLogger.LogAsync("Executable not found after extraction", $"{nameof(UpdateHandler)}.cs", Importance.Warning);
            return false;
        }

        await BetterLogger.LogAsync("Update package downloaded and extracted", $"{nameof(UpdateHandler)}.cs", Importance.Info);

        return await TryRunNewExecutable(latestRelease, exePath);
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
            await BetterLogger.LogAsync($"Error starting new process: {ex.Message}", $"{nameof(UpdateHandler)}.cs", Importance.Error);
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

        await BetterLogger.LogAsync($"Downloaded asset to: {filePath}", $"{nameof(UpdateHandler)}.cs", Importance.Info);
        return filePath;
    }

    private static async Task DeleteOldFilesAsync(string directory)
    {
        var rarFiles = Directory.GetFiles(directory, "*.rar");
        var extractedFiles = Directory.GetFiles(directory, "*.*").Where(f => !f.EndsWith(".rar")).ToArray();

        foreach (var file in rarFiles) File.Delete(file);
        foreach (var file in extractedFiles) File.Delete(file);

        await BetterLogger.LogAsync("Old files deleted", $"{nameof(UpdateHandler)}.cs", Importance.Info);
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
                    await BetterLogger.LogAsync($"No files found in RAR archive: {rarPath}", $"{nameof(UpdateHandler)}.cs",
                        Importance.Warning);
                    return null;
                }

                foreach (var entry in entries)
                {
                    await BetterLogger.LogAsync($"Extracting entry: {entry.Key}", $"{nameof(UpdateHandler)}.cs", Importance.Info);
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
                await BetterLogger.LogAsync($"Extracted file: {file}", $"{nameof(UpdateHandler)}.cs", Importance.Info);

            // Search for the executable in all subdirectories
            var exeFile = Directory.GetFiles(tempDirectory, "*.exe", SearchOption.AllDirectories).FirstOrDefault();
            await BetterLogger.LogAsync($"RAR extracted, executable found: {exeFile}", $"{nameof(UpdateHandler)}.cs",
                Importance.Info);
            return exeFile;
        }
        catch (UnauthorizedAccessException ex)
        {
            await BetterLogger.LogAsync($"Access to the path is denied: {ex.Message}", $"{nameof(UpdateHandler)}.cs",
                Importance.Error);
            return null;
        }
        catch (Exception ex)
        {
            await BetterLogger.LogAsync($"Error extracting RAR: {ex.Message}", $"{nameof(UpdateHandler)}.cs", Importance.Error);
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
            await BetterLogger.LogAsync($"Fetched latest release: {latestRelease?.TagName}", $"{nameof(UpdateHandler)}.cs",
                Importance.Info);
            return latestRelease;
        }
        catch (Exception ex)
        {
            await BetterLogger.LogAsync($"Error fetching latest release: {ex.Message}", $"{nameof(UpdateHandler)}.cs",
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