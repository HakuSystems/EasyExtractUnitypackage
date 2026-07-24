using System.Runtime.InteropServices;

namespace EasyExtractCrossPlatform.Services;

internal static class SearchUtilities
{
    /// <summary>
    ///     Starts draining a redirected stderr pipe immediately. A redirected pipe that nobody reads
    ///     fills up after a few kilobytes, at which point the child process blocks on its next write
    ///     and never exits, so the caller hangs in WaitForExit. Search backends hit this easily:
    ///     'find /' emits a permission denied line for every directory it cannot read.
    /// </summary>
    public static Task<string> BeginDrainingStandardError(Process process)
    {
        ArgumentNullException.ThrowIfNull(process);

        return process.StandardError.ReadToEndAsync();
    }

    /// <summary>
    ///     Collects the stderr text started by <see cref="BeginDrainingStandardError" />. Never throws,
    ///     because a missing error message must not mask the exit code the caller wants to report.
    /// </summary>
    public static string CollectStandardError(Task<string> drainTask)
    {
        ArgumentNullException.ThrowIfNull(drainTask);

        try
        {
            return drainTask.GetAwaiter().GetResult() ?? string.Empty;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

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