using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Windows;
using EasyExtract.Config;
using Newtonsoft.Json;

namespace EasyExtract.Updater;

public class UpdateHandler
{
    private readonly HttpClient _client = new();
    private readonly string RepoName = "EasyExtractUnitypackage";
    private readonly string RepoOwner = "HakuSystems";

    public UpdateHandler()
    {
        _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("EasyExtract", CurrentVersion));
    }

    private ConfigModel Config { get; } = new();
    private static string? LatestVersion { get; set; }
    private string NewAppName => $"EasyExtractUnitypackageV{LatestVersion}";
    private Uri? UpdateUri { get; set; }
    private string CurrentVersion => Config.CurrentVersion;

    public async Task<bool> IsUptoDate()
    {
        var response = await _client.GetAsync($"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest");

        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            var release = JsonConvert.DeserializeObject<Release>(content);

            LatestVersion = release?.TagName;
            UpdateUri = !string.IsNullOrEmpty(release?.HtmlUrl) ? new Uri(release.HtmlUrl) : null;

            return CurrentVersion == LatestVersion; // Equality check
        }

        await Console.Error.WriteLineAsync($"Error checking for updates: {response.StatusCode}");
        return true;
    }

    public async Task<bool> Update()
    {
        try
        {
            if (UpdateUri == null)
            {
                await Console.Error.WriteLineAsync("No update URI available.");
                return false; // No update available
            }

            var response =
                await _client.GetAsync($"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest");
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            var release = JsonConvert.DeserializeObject<Release>(content);

            // Fetch assets for the release
            var assetsResponse = await _client.GetAsync(release.AssetsUrl); // Uses the new AssetsUrl property
            assetsResponse.EnsureSuccessStatusCode();
            var assetsContent = await assetsResponse.Content.ReadAsStringAsync();
            var assets = JsonConvert.DeserializeObject<Asset[]>(assetsContent);

            // Filter for .exe asset
            var exeAsset = assets.FirstOrDefault(a => a.Name.EndsWith(".exe"));
            if (exeAsset == null)
            {
                await Console.Error.WriteLineAsync("No .exe asset found in the release.");
                return false;
            }

            var tempPath = ConfigModel.DefaultTempPath;
            var tempFile = Path.Combine(tempPath, exeAsset.Name);

            // Download the executable file
            using (var downloadResponse = await _client.GetAsync(exeAsset.DownloadUrl))
            {
                downloadResponse.EnsureSuccessStatusCode();
                await using var fileStream = new FileStream(tempFile, FileMode.Create);
                await downloadResponse.Content.CopyToAsync(fileStream);
            }

            // Start the new executable (tempFile) and close the current application
            var newProcess = Process.Start(new ProcessStartInfo(tempFile) { UseShellExecute = true });
            if (newProcess != null)
            {
                await Task.Delay(2000); // Delay to allow the new process to start

                // Close the current application
                Application.Current?.Dispatcher.Invoke(Application.Current.Shutdown);

                return true;
            }

            throw new Exception("Failed to start the new process.");
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Update failed: {ex.Message}");
            return false;
        }
    }
}

public class Asset
{
    [JsonProperty("name")] public string Name { get; set; }

    [JsonProperty("browser_download_url")] public string DownloadUrl { get; set; }
}

public class Release
{
    [JsonProperty("tag_name")] public string TagName { get; set; }

    [JsonProperty("html_url")] public string HtmlUrl { get; set; }

    [JsonProperty("assets_url")] public string AssetsUrl { get; set; }
}