using System.Text;
using EasyExtract.Models;
using EasyExtract.Utilities;

namespace EasyExtract.Services;

public record EverythingValidation
{
    private const string DllName = "Everything64.dll";

    private const string DownloadUrl =
        "\u0068\u0074\u0074\u0070\u0073\u003a\u002f\u002f\u0067\u0069\u0074\u0068\u0075\u0062\u002e\u0063\u006f\u006d\u002f\u0048\u0061\u006b\u0075\u0053\u0079\u0073\u0074\u0065\u006d\u0073\u002f\u0045\u0061\u0073\u0079\u0045\u0078\u0074\u0072\u0061\u0063\u0074\u0055\u006e\u0069\u0074\u0079\u0070\u0061\u0063\u006b\u0061\u0067\u0065\u002f\u0072\u0061\u0077\u002f\u006d\u0061\u0069\u006e\u002f\u0045\u0076\u0065\u0072\u0079\u0074\u0068\u0069\u006e\u0067\u0036\u0034\u002e\u0064\u006c\u006c";

    private const string ProcessName = "Everything";

    public static async Task<bool> AreSystemRequirementsMet()
    {
        var elapsedTime = Stopwatch.StartNew();
        await BetterLogger.LogAsync("Checking system requirements...", $"{nameof(EverythingValidation)}.cs",
            Importance.Info); // Log start check
        var result = await Is64BitOperatingSystem() && await IsProcessRunning(ProcessName) && await DoesDllExist() &&
                     await CopyDllIfNecessary();
        elapsedTime.Stop();
        await BetterLogger.LogAsync($"System requirements checked in {elapsedTime.ElapsedMilliseconds}ms.",
            $"{nameof(EverythingValidation)}.cs", Importance.Info); // Log end check
        return result;
    }

    public static async Task<string> AreSystemRequirementsMetString()
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
        await BetterLogger.LogAsync("Checked system requirements string.", $"{nameof(EverythingValidation)}.cs",
            Importance.Info); // Log check string
        return missing.ToString();
    }

    private static async Task<bool> Is64BitOperatingSystem()
    {
        var result = Environment.Is64BitOperatingSystem;
        await BetterLogger.LogAsync($"Is64BitOperatingSystem: {result}", $"{nameof(EverythingValidation)}.cs",
            Importance.Info); // Log OS check
        return result;
    }

    private static async Task<bool> IsProcessRunning(string processName)
    {
        var result = Process.GetProcessesByName(processName).Length > 0;
        await BetterLogger.LogAsync($"IsProcessRunning('{processName}'): {result}", $"{nameof(EverythingValidation)}.cs",
            Importance.Info); // Log process check
        return result;
    }

    private static async Task<bool> DoesDllExist()
    {
        var dllPath = GetDllPath();
        if (File.Exists(await dllPath))
        {
            await BetterLogger.LogAsync($"DLL exists at path: {dllPath}", $"{nameof(EverythingValidation)}.cs",
                Importance.Info); // Log DLL existence
            return true;
        }

        try
        {
            var elapsedTime = Stopwatch.StartNew();
            await DownloadDllAsync(await dllPath);
            elapsedTime.Stop();
            await BetterLogger.LogAsync($"Everything64.dll downloaded in {elapsedTime.ElapsedMilliseconds}ms.",
                $"{nameof(EverythingValidation)}.cs", Importance.Info); // Log DLL download
            return true;
        }
        catch (Exception ex)
        {
            await BetterLogger.LogAsync($"Failed to download Everything64.dll: {ex.Message}", $"{nameof(EverythingValidation)}.cs",
                Importance.Error); // Log DLL download failure
            return false;
        }
    }

    private static async Task DownloadDllAsync(string dllPath)
    {
        using var client = new HttpClient();
        var response = await client.GetAsync(DownloadUrl);
        await using var fs = new FileStream(dllPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await response.Content.CopyToAsync(fs);
    }

    private static async Task<string> GetDllPath()
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EasyExtract",
            "ThirdParty", DllName);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? throw new InvalidOperationException());
        await BetterLogger.LogAsync($"DLL path determined: {path}", $"{nameof(EverythingValidation)}.cs",
            Importance.Info); // Log DLL path
        return path;
    }

    private static async Task<bool> CopyDllIfNecessary()
    {
        var dllPath = GetDllPath();
        var currentPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (currentPath != null)
        {
            var destinationPath = Path.Combine(currentPath, DllName);
            if (File.Exists(destinationPath))
            {
                await BetterLogger.LogAsync($"DLL already exists at destination: {destinationPath}",
                    $"{nameof(EverythingValidation)}.cs",
                    Importance.Info); // Log DLL already exists
                return true;
            }

            try
            {
                File.Copy(await dllPath, destinationPath);
                await BetterLogger.LogAsync("Everything64.dll moved to destination.", $"{nameof(EverythingValidation)}.cs",
                    Importance.Info); // Log DLL move
                return true;
            }
            catch (Exception ex)
            {
                await BetterLogger.LogAsync($"Failed to copy Everything64.dll: {ex.Message}", $"{nameof(EverythingValidation)}.cs",
                    Importance.Error); // Log DLL copy failure
                return false;
            }
        }

        return false;
    }
}