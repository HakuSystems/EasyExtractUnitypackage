using System.Text;
using EasyExtract.Config.Models;
using EasyExtract.Utilities;

namespace EasyExtract.Services;

public static class EverythingValidation
{
    private const string DllName = "Everything64.dll";

    private const string DownloadUrl =
        "\u0068\u0074\u0074\u0070\u0073\u003a\u002f\u002f\u0067\u0069\u0074\u0068\u0075\u0062\u002e\u0063\u006f\u006d\u002f\u0048\u0061\u006b\u0075\u0053\u0079\u0073\u0074\u0065\u006d\u0073\u002f\u0045\u0061\u0073\u0079\u0045\u0078\u0074\u0072\u0061\u0063\u0074\u0055\u006e\u0069\u0074\u0079\u0070\u0061\u0063\u006b\u0061\u0067\u0065\u002f\u0072\u0061\u0077\u002f\u006d\u0061\u0069\u006e\u002f\u0045\u0076\u0065\u0072\u0079\u0074\u0068\u0069\u006e\u0067\u0036\u0034\u002e\u0064\u006c\u006c";

    private const string ProcessName = "Everything";

    /// <summary>
    ///     Checks all system requirements asynchronously.
    /// </summary>
    public static async Task<bool> AreSystemRequirementsMetAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        await BetterLogger.LogAsync("Checking system requirements...", Importance.Info);

        var requirementsMet = await Is64BitOperatingSystemAsync() &&
                              await IsProcessRunningAsync(ProcessName) &&
                              await EnsureDllExistsAsync() &&
                              await CopyDllIfNecessaryAsync();

        stopwatch.Stop();
        await BetterLogger.LogAsync($"System requirements checked in {stopwatch.ElapsedMilliseconds}ms.",
            Importance.Info);
        return requirementsMet;
    }

    /// <summary>
    ///     Returns a detailed status string listing any missing requirements.
    /// </summary>
    public static async Task<string> GetSystemRequirementsStatusAsync()
    {
        var status = new StringBuilder();

        if (!await Is64BitOperatingSystemAsync())
            status.AppendLine("System requirement not met: Requires a 64-bit operating system.");

        if (!await IsProcessRunningAsync(ProcessName))
            status.AppendLine("System requirement not met: 'SearchEverything' process isn't running. Please start it.");

        if (!await EnsureDllExistsAsync())
            status.AppendLine("System requirement not met: 'Everything DLL' is missing.");

        if (!await CopyDllIfNecessaryAsync())
            status.AppendLine("System requirement not met: Unable to copy the required DLL.");

        await BetterLogger.LogAsync("Checked system requirements status.", Importance.Info);
        return status.ToString();
    }

    private static async Task<bool> Is64BitOperatingSystemAsync()
    {
        var is64Bit = Environment.Is64BitOperatingSystem;
        await BetterLogger.LogAsync($"Is64BitOperatingSystem: {is64Bit}", Importance.Info);
        return is64Bit;
    }

    private static async Task<bool> IsProcessRunningAsync(string processName)
    {
        var isRunning = Process.GetProcessesByName(processName).Length > 0;
        await BetterLogger.LogAsync($"IsProcessRunning('{processName}'): {isRunning}", Importance.Info);
        return isRunning;
    }

    private static async Task<bool> EnsureDllExistsAsync()
    {
        var dllPath = await GetDllPathAsync();
        if (File.Exists(dllPath))
        {
            await BetterLogger.LogAsync($"DLL exists at path: {dllPath}", Importance.Info);
            return true;
        }

        try
        {
            var stopwatch = Stopwatch.StartNew();
            await DownloadDllAsync(dllPath);
            stopwatch.Stop();
            await BetterLogger.LogAsync($"Everything64.dll downloaded in {stopwatch.ElapsedMilliseconds}ms.",
                Importance.Info);
            return true;
        }
        catch (Exception ex)
        {
            await BetterLogger.LogAsync($"Failed to download Everything64.dll: {ex.Message}", Importance.Error);
            return false;
        }
    }

    private static async Task DownloadDllAsync(string dllPath)
    {
        using var client = new HttpClient();
        using var response = await client.GetAsync(DownloadUrl);
        response.EnsureSuccessStatusCode();
        await using var fs = new FileStream(dllPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await response.Content.CopyToAsync(fs);
    }

    private static async Task<string> GetDllPathAsync()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var directory = Path.Combine(appDataPath, "EasyExtract", "ThirdParty");
        Directory.CreateDirectory(directory);
        var dllPath = Path.Combine(directory, DllName);
        await BetterLogger.LogAsync($"DLL path determined: {dllPath}", Importance.Info);
        return dllPath;
    }

    private static async Task<bool> CopyDllIfNecessaryAsync()
    {
        var sourcePath = await GetDllPathAsync();
        var currentPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (!string.IsNullOrEmpty(currentPath))
        {
            var destinationPath = Path.Combine(currentPath, DllName);
            if (File.Exists(destinationPath))
            {
                await BetterLogger.LogAsync($"DLL already exists at destination: {destinationPath}", Importance.Info);
                return true;
            }

            try
            {
                File.Copy(sourcePath, destinationPath, true);
                await BetterLogger.LogAsync("Everything64.dll copied to destination.", Importance.Info);
                return true;
            }
            catch (Exception ex)
            {
                await BetterLogger.LogAsync($"Failed to copy Everything64.dll: {ex.Message}", Importance.Error);
                return false;
            }
        }

        return false;
    }
}