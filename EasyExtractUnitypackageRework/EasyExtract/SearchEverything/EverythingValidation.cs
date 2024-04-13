using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;

namespace EasyExtract.SearchEverything;

public class EverythingValidation
{
    private const string DllName = "Everything64.dll";

    private const string DownloadUrl =
        "https://github.com/HakuSystems/EasyExtractUnitypackage/raw/main/Everything64.dll";

    private const string ProcessName = "Everything";

    public static bool AreSystemRequirementsMet()
    {
        var elapsedTime = Stopwatch.StartNew();
        Console.WriteLine("Checking system requirements...");
        var result = Is64BitOperatingSystem() && IsProcessRunning(ProcessName) && DoesDllExist() &&
                     CopyDllIfNecessary();
        elapsedTime.Stop();
        Console.WriteLine($"System requirements checked in {elapsedTime.ElapsedMilliseconds}ms.");
        return result;
    }

    public static string AreSystemRequirementsMetString()
    {
        var missing = new StringBuilder();
        if (!Is64BitOperatingSystem())
            missing.AppendLine("System requirement not met: Requires a 64-bit operating system.");
        if (!IsProcessRunning(ProcessName))
            missing.AppendLine(
                "System requirement not met: 'SearchEverything' process isn't running. Please start it.");
        if (!DoesDllExist()) missing.AppendLine("System requirement not met: 'Everything DLL' is missing.");
        if (!CopyDllIfNecessary()) missing.AppendLine("System requirement not met: Unable to copy the required DLL.");
        return missing.ToString();
    }

    private static bool Is64BitOperatingSystem()
    {
        return Environment.Is64BitOperatingSystem;
    }

    private static bool IsProcessRunning(string processName)
    {
        return Process.GetProcessesByName(processName).Length > 0;
    }

    private static bool DoesDllExist()
    {
        var dllPath = GetDllPath();
        if (File.Exists(dllPath)) return true;
        try
        {
            var elapsedTime = Stopwatch.StartNew();
            DownloadDll(dllPath);
            elapsedTime.Stop();
            Console.WriteLine($"Everything64.dll downloaded in {elapsedTime.ElapsedMilliseconds}ms.");
            return true;
        }
        catch (Exception)
        {
            // Handle exception
            return false;
        }
    }

    private static void DownloadDll(string dllPath)
    {
        using (var client = new WebClient())
        {
            client.DownloadFile(new Uri(DownloadUrl), dllPath);
        }
    }

    private static string GetDllPath()
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EasyExtract",
            "ThirdParty", DllName);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        return path;
    }

    private static bool CopyDllIfNecessary()
    {
        var dllPath = GetDllPath();
        var currentPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        var destinationPath = Path.Combine(currentPath, DllName);
        if (File.Exists(destinationPath)) return true;

        try
        {
            File.Copy(dllPath, destinationPath);
            Console.WriteLine("Everything64.dll moved.");
            return true;
        }
        catch (Exception)
        {
            // Handle exception
            return false;
        }
    }
}