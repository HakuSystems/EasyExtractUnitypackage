using System.Text;
using EasyExtract.Config;
using EasyExtract.Utilities;

namespace EasyExtract.Services.SearchEverything;

public class EverythingValidation
{
    private const string DllName = "Everything64.dll";

    private const string DownloadUrl =
        "https://github.com/HakuSystems/EasyExtractUnitypackage/raw/main/Everything64.dll";

    private const string ProcessName = "Everything";
    private readonly BetterLogger _logger = new();

    public async Task<bool> AreSystemRequirementsMet()
    {
        var elapsedTime = Stopwatch.StartNew();
        await _logger.LogAsync("Checking system requirements...", "EverythingValidation.cs",
            Importance.Info); // Log start check
        var result = await Is64BitOperatingSystem() && await IsProcessRunning(ProcessName) && await DoesDllExist() &&
                     await CopyDllIfNecessary();
        elapsedTime.Stop();
        await _logger.LogAsync($"System requirements checked in {elapsedTime.ElapsedMilliseconds}ms.",
            "EverythingValidation.cs", Importance.Info); // Log end check
        return result;
    }

    public async Task<string> AreSystemRequirementsMetString()
    {
        var missing = new StringBuilder();
        if (!await Is64BitOperatingSystem())
            missing.AppendLine("System requirement not met: Requires a 64-bit operating system.");
        if (!await IsProcessRunning(ProcessName))
            missing.AppendLine(
                "System requirement not met: 'SearchEverything' process isn't running. Please start it.");
        if (!await DoesDllExist()) missing.AppendLine("System requirement not met: 'Everything DLL' is missing.");
        if (!await CopyDllIfNecessary())
            missing.AppendLine("System requirement not met: Unable to copy the required DLL.");
        await _logger.LogAsync("Checked system requirements string.", "EverythingValidation.cs",
            Importance.Info); // Log check string
        return missing.ToString();
    }

    private async Task<bool> Is64BitOperatingSystem()
    {
        var result = Environment.Is64BitOperatingSystem;
        await _logger.LogAsync($"Is64BitOperatingSystem: {result}", "EverythingValidation.cs",
            Importance.Info); // Log OS check
        return result;
    }

    private async Task<bool> IsProcessRunning(string processName)
    {
        var result = Process.GetProcessesByName(processName).Length > 0;
        await _logger.LogAsync($"IsProcessRunning('{processName}'): {result}", "EverythingValidation.cs",
            Importance.Info); // Log process check
        return result;
    }

    private async Task<bool> DoesDllExist()
    {
        var dllPath = GetDllPath();
        if (File.Exists(await dllPath))
        {
            await _logger.LogAsync($"DLL exists at path: {dllPath}", "EverythingValidation.cs",
                Importance.Info); // Log DLL existence
            return true;
        }

        try
        {
            var elapsedTime = Stopwatch.StartNew();
            DownloadDllAsync(await dllPath);
            elapsedTime.Stop();
            await _logger.LogAsync($"Everything64.dll downloaded in {elapsedTime.ElapsedMilliseconds}ms.",
                "EverythingValidation.cs", Importance.Info); // Log DLL download
            return true;
        }
        catch (Exception ex)
        {
            await _logger.LogAsync($"Failed to download Everything64.dll: {ex.Message}", "EverythingValidation.cs",
                Importance.Error); // Log DLL download failure
            return false;
        }
    }

    private async void DownloadDllAsync(string dllPath)
    {
        using var client = new HttpClient();
        var response = await client.GetAsync(DownloadUrl);
        await using var fs = new FileStream(dllPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await response.Content.CopyToAsync(fs);
    }

    private async Task<string> GetDllPath()
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EasyExtract",
            "ThirdParty", DllName);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        await _logger.LogAsync($"DLL path determined: {path}", "EverythingValidation.cs",
            Importance.Info); // Log DLL path
        return path;
    }

    private async Task<bool> CopyDllIfNecessary()
    {
        var dllPath = GetDllPath();
        var currentPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        var destinationPath = Path.Combine(currentPath, DllName);
        if (File.Exists(destinationPath))
        {
            await _logger.LogAsync($"DLL already exists at destination: {destinationPath}", "EverythingValidation.cs",
                Importance.Info); // Log DLL already exists
            return true;
        }

        try
        {
            File.Copy(await dllPath, destinationPath);
            await _logger.LogAsync("Everything64.dll moved to destination.", "EverythingValidation.cs",
                Importance.Info); // Log DLL move
            return true;
        }
        catch (Exception ex)
        {
            await _logger.LogAsync($"Failed to copy Everything64.dll: {ex.Message}", "EverythingValidation.cs",
                Importance.Error); // Log DLL copy failure
            return false;
        }
    }
}