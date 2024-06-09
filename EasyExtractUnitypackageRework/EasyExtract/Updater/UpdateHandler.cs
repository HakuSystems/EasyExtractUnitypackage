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
    private const string RepoName = "EasyExtractUnitypackage";
    private const string RepoOwner = "HakuSystems";
    private ConfigModel Config { get; } = new();
    private string CurrentVersion => Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";

    public async Task<bool> IsUpToDate()
    {
        var latestRelease = await GetLatestReleaseAsync();
        if (latestRelease != null)
        {
            var latestVersion = latestRelease.TagName.TrimStart('v', 'V'); // Removing both 'v' and 'V'
            return Version.TryParse(latestVersion, out var latest) &&
                   Version.TryParse(CurrentVersion, out var current) &&
                   latest <= current;
        }

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
                if (exePath != null)
                {
                    Process.Start(exePath);
                    Environment.Exit(0);
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }
        else
        {
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

        using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await response.Content.CopyToAsync(fs);
        }

        return filePath;
    }

    private void DeleteOldFiles(string directory)
    {
        var rarFiles = Directory.GetFiles(directory, "*.rar");
        var extractedFiles = Directory.GetFiles(directory, "*.*").Where(f => !f.EndsWith(".rar")).ToArray();

        foreach (var file in rarFiles) File.Delete(file);

        foreach (var file in extractedFiles) File.Delete(file);
    }

    private string ExtractRar(string rarPath)
    {
        var currentDirectory = AppDomain.CurrentDomain.BaseDirectory;
        using (var archive = ArchiveFactory.Open(rarPath))
        {
            foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
                entry.WriteToDirectory(currentDirectory,
                    new ExtractionOptions { ExtractFullPath = true, Overwrite = true });
        }

        var exeFile = Directory.GetFiles(currentDirectory, "*.exe").FirstOrDefault();
        return exeFile;
    }

    private async Task<Release> GetLatestReleaseAsync()
    {
        try
        {
            var client = new GitHubClient(new ProductHeaderValue("GitHubUpdater"));
            var releases = await client.Repository.Release.GetAll(RepoOwner, RepoName);
            return releases.FirstOrDefault();
        }
        catch (Exception ex)
        {
            await Console.Error.WriteAsync(ex.Message);
            return null;
        }
    }
}