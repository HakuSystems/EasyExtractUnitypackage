using System;
using System.IO;
using System.Runtime.InteropServices;
using EasyExtractCrossPlatform.Models;

namespace EasyExtractCrossPlatform.Services;

internal static class SearchUtilities
{
    public static string? TryResolveExecutable(string executableName)
    {
        if (string.IsNullOrWhiteSpace(executableName))
            return null;

        var pathVariable = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathVariable))
            return null;

        foreach (var segment in pathVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = segment.Trim();
            if (trimmed.Length == 0)
                continue;

            var candidate = Path.Combine(trimmed, executableName);
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    public static EverythingSearchResult? BuildResultFromPath(string fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
            return null;

        if (!fullPath.EndsWith(".unitypackage", StringComparison.OrdinalIgnoreCase))
            return null;

        try
        {
            var fileInfo = new FileInfo(fullPath);
            if (!fileInfo.Exists || fileInfo.Length <= 0)
                return null;

            var fileName = fileInfo.Name;
            var lastWriteUtc = fileInfo.LastWriteTimeUtc;
            var lastModified = lastWriteUtc == DateTime.MinValue
                ? (DateTimeOffset?)null
                : new DateTimeOffset(DateTime.SpecifyKind(lastWriteUtc, DateTimeKind.Utc));

            return new EverythingSearchResult(
                fileName,
                fileInfo.FullName,
                false,
                true,
                fileInfo.Length,
                lastModified);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (ExternalException)
        {
            return null;
        }
    }
}