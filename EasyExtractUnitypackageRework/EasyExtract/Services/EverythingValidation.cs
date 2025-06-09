using System.Text;
using EasyExtract.Utilities.Logger;

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
        var context = new Dictionary<string, object>();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            BetterLogger.LogWithContext("Checking system requirements...", context, LogLevel.Info,
                "SystemRequirements");

            var is64BitOs = await Is64BitOperatingSystemAsync();
            context["Is64BitOS"] = is64BitOs;

            var isProcessRunning = await IsProcessRunningAsync(ProcessName);
            context["ProcessRunning"] = isProcessRunning;

            var dllExists = await EnsureDllExistsAsync();
            context["DllExists"] = dllExists;

            var dllCopied = await CopyDllIfNecessaryAsync();
            context["DllCopied"] = dllCopied;

            var requirementsMet = is64BitOs && isProcessRunning && dllExists && dllCopied;
            context["RequirementsMet"] = requirementsMet;

            stopwatch.Stop();
            context["DurationMs"] = stopwatch.ElapsedMilliseconds;
            BetterLogger.LogWithContext($"System requirements checked in {stopwatch.ElapsedMilliseconds}ms.",
                context, LogLevel.Info, "SystemRequirements");
            return requirementsMet;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            context["DurationMs"] = stopwatch.ElapsedMilliseconds;
            context["Error"] = ex.Message;
            BetterLogger.Exception(ex, "Error checking system requirements", "SystemRequirements");
            return false;
        }
    }

    /// <summary>
    ///     Returns a detailed status string listing any missing requirements.
    /// </summary>
    public static async Task<string> GetSystemRequirementsStatusAsync()
    {
        var status = new StringBuilder();
        var context = new Dictionary<string, object>();

        var is64BitOs = await Is64BitOperatingSystemAsync();
        context["Is64BitOS"] = is64BitOs;
        if (!is64BitOs)
            status.AppendLine("System requirement not met: Requires a 64-bit operating system.");

        var isProcessRunning = await IsProcessRunningAsync(ProcessName);
        context["ProcessRunning"] = isProcessRunning;
        if (!isProcessRunning)
            status.AppendLine("System requirement not met: 'SearchEverything' process isn't running. Please start it.");

        var dllExists = await EnsureDllExistsAsync();
        context["DllExists"] = dllExists;
        if (!dllExists)
            status.AppendLine("System requirement not met: 'Everything DLL' is missing.");

        var dllCopied = await CopyDllIfNecessaryAsync();
        context["DllCopied"] = dllCopied;
        if (!dllCopied)
            status.AppendLine("System requirement not met: Unable to copy the required DLL.");

        BetterLogger.LogWithContext("Checked system requirements status.", context, LogLevel.Info,
            "SystemRequirements");
        return status.ToString();
    }

    private static async Task<bool> Is64BitOperatingSystemAsync()
    {
        var context = new Dictionary<string, object>();
        var is64Bit = Environment.Is64BitOperatingSystem;
        context["Is64BitOS"] = is64Bit;
        BetterLogger.LogWithContext($"Is64BitOperatingSystem: {is64Bit}", context, LogLevel.Debug,
            "SystemRequirements");
        return is64Bit;
    }

    private static async Task<bool> IsProcessRunningAsync(string processName)
    {
        var context = new Dictionary<string, object>
        {
            ["ProcessName"] = processName
        };

        var isRunning = Process.GetProcessesByName(processName).Length > 0;
        context["IsRunning"] = isRunning;

        BetterLogger.LogWithContext($"IsProcessRunning('{processName}'): {isRunning}", context, LogLevel.Debug,
            "SystemRequirements");
        return isRunning;
    }

    private static async Task<bool> EnsureDllExistsAsync()
    {
        var context = new Dictionary<string, object>();
        var dllPath = await GetDllPathAsync();
        context["DllPath"] = dllPath;

        if (File.Exists(dllPath))
        {
            BetterLogger.LogWithContext($"DLL exists at path: {dllPath}", context, LogLevel.Debug,
                "SystemRequirements");
            return true;
        }

        try
        {
            var stopwatch = Stopwatch.StartNew();
            await DownloadDllAsync(dllPath);
            stopwatch.Stop();

            context["DownloadDurationMs"] = stopwatch.ElapsedMilliseconds;
            BetterLogger.LogWithContext($"Everything64.dll downloaded in {stopwatch.ElapsedMilliseconds}ms.",
                context, LogLevel.Info, "SystemRequirements");
            return true;
        }
        catch (Exception ex)
        {
            context["Error"] = ex.Message;
            BetterLogger.Exception(ex, "Failed to download Everything64.dll", "SystemRequirements");
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
        var context = new Dictionary<string, object>();
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var directory = Path.Combine(appDataPath, "EasyExtract", "ThirdParty");
        Directory.CreateDirectory(directory);
        var dllPath = Path.Combine(directory, DllName);

        context["DllPath"] = dllPath;
        BetterLogger.LogWithContext($"DLL path determined: {dllPath}", context, LogLevel.Debug, "SystemRequirements");
        return dllPath;
    }

    private static async Task<bool> CopyDllIfNecessaryAsync()
    {
        var context = new Dictionary<string, object>();
        var sourcePath = await GetDllPathAsync();
        var currentPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        context["SourcePath"] = sourcePath;
        context["CurrentPath"] = currentPath;

        if (!string.IsNullOrEmpty(currentPath))
        {
            var destinationPath = Path.Combine(currentPath, DllName);
            context["DestinationPath"] = destinationPath;

            if (File.Exists(destinationPath))
            {
                BetterLogger.LogWithContext($"DLL already exists at destination: {destinationPath}",
                    context, LogLevel.Debug, "SystemRequirements");
                return true;
            }

            try
            {
                File.Copy(sourcePath, destinationPath, true);
                BetterLogger.LogWithContext("Everything64.dll copied to destination.",
                    context, LogLevel.Info, "SystemRequirements");
                return true;
            }
            catch (Exception ex)
            {
                context["Error"] = ex.Message;
                BetterLogger.Exception(ex, "Failed to copy Everything64.dll", "SystemRequirements");
                return false;
            }
        }

        return false;
    }
}