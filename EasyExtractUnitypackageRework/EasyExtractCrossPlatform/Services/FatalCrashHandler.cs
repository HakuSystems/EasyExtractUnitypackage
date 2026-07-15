using System.Runtime.InteropServices;
using System.Text;

namespace EasyExtractCrossPlatform.Services;

/// <summary>
///     Last line of defense for exceptions that escape the Avalonia lifetime. Logs the failure,
///     gives the error report a chance to leave the process and tells the user what happened
///     instead of dying silently through Windows Error Reporting.
/// </summary>
internal static class FatalCrashHandler
{
    private const int MaxInspectionDepth = 16;

    // MB_OK | MB_ICONERROR | MB_SETFOREGROUND | MB_TOPMOST
    private const uint MessageBoxFlags = 0x00050010;

    public static void Handle(Exception exception, string context)
    {
        if (IsOutOfMemory(exception))
            TryReclaimMemory();

        try
        {
            LoggingService.LogError(context, exception);
        }
        catch
        {
            // The process is already dying; the crash handler must never crash.
        }

        string? logFilePath = null;
        try
        {
            logFilePath = LoggingService.LogFilePath;
        }
        catch
        {
            // Logging never initialized; the dialog simply omits the path.
        }

        var message = BuildUserMessage(exception, logFilePath);

        // The blocking dialog doubles as a grace period for the webhook dispatch. Without a
        // dialog (non-Windows or dialog failure) wait briefly so the report can leave.
        if (!TryShowNativeErrorDialog(message))
            Thread.Sleep(2000);
    }

    internal static bool IsOutOfMemory(Exception? exception)
    {
        return ContainsOutOfMemory(exception, 0);
    }

    internal static string BuildUserMessage(Exception exception, string? logFilePath)
    {
        var builder = new StringBuilder();

        if (IsOutOfMemory(exception))
            builder.AppendLine("EasyExtract had to close because the system is critically low on memory.")
                .AppendLine()
                .AppendLine("Close some other applications or restart your PC, then try again.");
        else
            builder.AppendLine("EasyExtract hit an unexpected error and had to close.")
                .AppendLine()
                .AppendLine("The problem was reported automatically.");

        if (!string.IsNullOrWhiteSpace(logFilePath))
            builder.AppendLine().Append("Details: ").Append(logFilePath);

        return builder.ToString().TrimEnd();
    }

    private static bool ContainsOutOfMemory(Exception? exception, int depth)
    {
        if (exception is null || depth >= MaxInspectionDepth)
            return false;

        if (exception is OutOfMemoryException)
            return true;

        if (exception is AggregateException aggregate)
            return aggregate.InnerExceptions.Any(inner => ContainsOutOfMemory(inner, depth + 1));

        return ContainsOutOfMemory(exception.InnerException, depth + 1);
    }

    private static void TryReclaimMemory()
    {
        try
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
        catch
        {
            // Best effort only.
        }
    }

    private static bool TryShowNativeErrorDialog(string message)
    {
        if (!OperatingSystem.IsWindows())
            return false;

        try
        {
            return MessageBoxW(IntPtr.Zero, message, "EasyExtract", MessageBoxFlags) != 0;
        }
        catch
        {
            return false;
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);
}
