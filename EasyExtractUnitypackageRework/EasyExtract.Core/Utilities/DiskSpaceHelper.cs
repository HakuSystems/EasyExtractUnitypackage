using System;
using System.IO;

namespace EasyExtract.Core.Utilities;

public static class DiskSpaceHelper
{
    private const int ErrorDiskFull = unchecked((int)0x80070070);
    private const int ErrorHandleDiskFull = unchecked((int)0x80070027);

    public static bool IsDiskFull(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is not IOException io)
                continue;

            if (IsDiskFullHResult(io.HResult) || MessageIndicatesDiskFull(io.Message))
                return true;
        }

        return false;
    }

    public static string BuildFriendlyMessage(string? targetPath)
    {
        var drive = TryFormatDrive(targetPath);
        return string.IsNullOrWhiteSpace(drive)
            ? "Not enough disk space to complete this action. Please free up space and try again."
            : $"Not enough disk space on {drive}. Please free up space or choose a different location and try again.";
    }

    private static bool IsDiskFullHResult(int hresult)
    {
        return hresult == ErrorDiskFull || hresult == ErrorHandleDiskFull;
    }

    private static bool MessageIndicatesDiskFull(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return false;

        return message.Contains("not enough space", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("disk full", StringComparison.OrdinalIgnoreCase);
    }

    private static string? TryFormatDrive(string? targetPath)
    {
        if (string.IsNullOrWhiteSpace(targetPath))
            return null;

        try
        {
            var root = Path.GetPathRoot(targetPath);
            if (string.IsNullOrWhiteSpace(root))
                return null;

            return root.TrimEnd('\\', '/');
        }
        catch
        {
            return null;
        }
    }
}
