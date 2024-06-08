using System.Diagnostics;
using System.IO;
using System.IO.Compression;
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

            var tempPath = ConfigModel.DefaultTempPath;
            var tempFile = Path.Combine(tempPath, NewAppName);

            // Download the update file
            using (var response = await _client.GetAsync(UpdateUri))
            {
                response.EnsureSuccessStatusCode();
                await using var fileStream = new FileStream(tempFile, FileMode.Create);
                await response.Content.CopyToAsync(fileStream);
            }

            // Extract if it's a ZIP, otherwise assume it's the executable
            string exePath;
            if (Path.GetExtension(tempFile).Equals(".zip", StringComparison.OrdinalIgnoreCase))
            {
                var extractPath = Path.Combine(tempPath, Path.GetFileNameWithoutExtension(tempFile));
                ZipFile.ExtractToDirectory(tempFile, extractPath);
                exePath = Directory.GetFiles(extractPath, "*.exe", SearchOption.AllDirectories).FirstOrDefault();

                if (exePath == null) throw new FileNotFoundException("Exe not found within the ZIP archive.");
            }
            else
            {
                exePath = tempFile;
            }

            var newProcess = Process.Start(new ProcessStartInfo(exePath) { UseShellExecute = true });
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
            // Consider showing a user-friendly error message here
            return false;
        }
    }
}

public class Release
{
    [JsonProperty("tag_name")] public string TagName { get; set; }

    [JsonProperty("html_url")] public string HtmlUrl { get; set; }
}