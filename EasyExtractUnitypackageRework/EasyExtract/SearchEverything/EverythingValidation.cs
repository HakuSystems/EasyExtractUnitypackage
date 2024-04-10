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
        Console.WriteLine("Checking system requirements...");
        var result = Is64BitOperatingSystem() && IsProcessRunning(ProcessName) && DoesDllExist() &&
                     CopyDllIfNecessary();
        Console.WriteLine("System requirements checked.");
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
        Console.WriteLine("Checking if operating system is 64-bit...");
        return Environment.Is64BitOperatingSystem;
    }

    private static bool IsProcessRunning(string processName)
    {
        Console.WriteLine($"Checking if process {processName} is running...");
        return Process.GetProcessesByName(processName).Length > 0;
    }

    private static bool DoesDllExist()
    {
        var dllPath = GetDllPath();
        if (File.Exists(dllPath)) return true;
        try
        {
            Console.WriteLine("Downloading DLL...");
            DownloadDll(dllPath);
            Console.WriteLine("DLL downloaded.");
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
            Console.WriteLine("Copying DLL...");
            File.Copy(dllPath, destinationPath);
            Console.WriteLine("DLL copied.");
            return true;
        }
        catch (Exception)
        {
            // Handle exception
            return false;
        }
    }
}