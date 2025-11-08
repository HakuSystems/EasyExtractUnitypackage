using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EasyExtractCrossPlatform.Models;

namespace EasyExtractCrossPlatform.Services;

public interface IEverythingSearchService
{
    int LastExcludedResultCount { get; }

    string AvailabilityHint { get; }

    Task<IReadOnlyList<EverythingSearchResult>> SearchAsync(string query, int maxResults,
        CancellationToken cancellationToken);

    Task<bool> IsAvailableAsync(CancellationToken cancellationToken);
}

public sealed class EverythingSearchService : IEverythingSearchService
{
    private const uint RequestFlags =
        EverythingNative.RequestFileName |
        EverythingNative.RequestPath |
        EverythingNative.RequestFullPathAndFileName |
        EverythingNative.RequestSize |
        EverythingNative.RequestDateModified |
        EverythingNative.RequestAttributes;

    private const int DefaultMaxResults = 200;

    private static readonly string[] ExcludedSearchScopes =
    {
        @"!""C:\$RECYCLE.BIN\""",
        @"!""E:\$RECYCLE.BIN\"""
    };

    private static readonly string[] ExcludedPathFragments =
    {
        @"\$RECYCLE.BIN\"
    };

    private readonly SemaphoreSlim _queryGate = new(1, 1);
    private string _availabilityHint = "Everything search has not been initialized.";
    private int _lastExcludedResultCount;

    public async Task<IReadOnlyList<EverythingSearchResult>> SearchAsync(
        string query,
        int maxResults,
        CancellationToken cancellationToken)
    {
        LoggingService.LogInformation($"Everything search requested. Query='{query}', maxResults={maxResults}.");

        await EverythingSdkBootstrapper.EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(query))
        {
            LoggingService.LogInformation("Everything search skipped because the query was empty.");
            return Array.Empty<EverythingSearchResult>();
        }

        maxResults = Math.Clamp(maxResults, 1, 2000);
        LoggingService.LogInformation($"Everything search normalized parameters. maxResults={maxResults}.");

        var stopwatch = Stopwatch.StartNew();

        var entered = false;
        try
        {
            await _queryGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            entered = true;

            var execution = await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return ExecuteSearchInternal(query, maxResults, cancellationToken);
            }, cancellationToken).ConfigureAwait(false);

            Volatile.Write(ref _lastExcludedResultCount, execution.FilteredCount);
            stopwatch.Stop();
            LoggingService.LogInformation(
                $"Everything search completed in {stopwatch.Elapsed.TotalMilliseconds:F0} ms. " +
                $"Results={execution.Results.Count}, excluded={execution.FilteredCount}.");
            return execution.Results;
        }
        catch (DllNotFoundException ex)
        {
            stopwatch.Stop();
            LoggingService.LogError("Everything search failed: SDK DLL not found.", ex);
            throw EverythingSearchException.MissingLibrary(ex);
        }
        catch (EntryPointNotFoundException ex)
        {
            stopwatch.Stop();
            LoggingService.LogError("Everything search failed: SDK DLL entry point mismatch.", ex);
            throw EverythingSearchException.MismatchedLibrary(ex);
        }
        finally
        {
            stopwatch.Stop();
            if (entered)
                _queryGate.Release();
        }
    }

    public int LastExcludedResultCount => Volatile.Read(ref _lastExcludedResultCount);

    public string AvailabilityHint => Volatile.Read(ref _availabilityHint);

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
    {
        LoggingService.LogInformation("Checking Everything search availability.");
        await EverythingSdkBootstrapper.EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var entered = false;
        try
        {
            await _queryGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            entered = true;

            var available = await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return EverythingNative.IsDbLoaded();
            }, cancellationToken).ConfigureAwait(false);

            Volatile.Write(ref _availabilityHint, available
                ? "Using Everything SDK for instant .unitypackage search."
                : "Everything is not running. Launch the Everything desktop app to enable .unitypackage search.");

            LoggingService.LogInformation(
                $"Everything availability check result: {(available ? "available" : "unavailable")}.");
            return available;
        }
        catch (DllNotFoundException ex)
        {
            Volatile.Write(ref _availabilityHint,
                "Everything SDK DLL is missing. Download the official Everything SDK or allow EasyExtract to fetch it automatically.");
            LoggingService.LogError("Everything availability check failed: SDK DLL missing.", ex);
            throw EverythingSearchException.MissingLibrary(ex);
        }
        catch (EntryPointNotFoundException ex)
        {
            Volatile.Write(ref _availabilityHint,
                "The Everything SDK DLL does not match this architecture. Replace it with the correct 32/64-bit build.");
            LoggingService.LogError("Everything availability check failed: SDK DLL entry point mismatch.", ex);
            throw EverythingSearchException.MismatchedLibrary(ex);
        }
        finally
        {
            if (entered)
                _queryGate.Release();
        }
    }

    private static (List<EverythingSearchResult> Results, int FilteredCount) ExecuteSearchInternal(
        string query,
        int maxResults,
        CancellationToken cancellationToken)
    {
        try
        {
            EverythingNative.Reset();
            EverythingNative.SetMatchPath(false);
            EverythingNative.SetRegex(false);
            EverythingNative.SetMatchCase(false);
            EverythingNative.SetMatchWholeWord(false);
            var trimmedQuery = query.Trim();
            var queryParts = new List<string>();

            if (!string.IsNullOrWhiteSpace(trimmedQuery))
                queryParts.Add(trimmedQuery);

            queryParts.Add("ext:unitypackage");
            queryParts.AddRange(ExcludedSearchScopes);

            var composedQuery = string.Join(' ', queryParts);

            EverythingNative.SetSearch(composedQuery);
            EverythingNative.SetMax((uint)(maxResults > 0 ? maxResults : DefaultMaxResults));
            EverythingNative.SetOffset(0);
            EverythingNative.SetRequestFlags(RequestFlags);
            EverythingNative.SetSort(EverythingNative.SortNameAscending);

            if (!EverythingNative.Query(true))
            {
                var code = EverythingNative.GetLastError();
                if (code != EverythingErrorCode.Ok)
                    throw EverythingSearchException.FromError(code);
            }

            var results = new List<EverythingSearchResult>();
            var excludedCount = 0;
            var totalResults = Math.Min(EverythingNative.GetNumResults(), (uint)maxResults);
            var pathBuffer = new StringBuilder(1024);

            for (uint index = 0; index < totalResults; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                pathBuffer.Clear();
                EverythingNative.GetResultFullPathName(index, pathBuffer, (uint)pathBuffer.Capacity);
                var fullPath = pathBuffer.ToString();
                if (string.IsNullOrWhiteSpace(fullPath))
                    continue;

                var namePtr = EverythingNative.GetResultFileName(index);
                var name = Marshal.PtrToStringUni(namePtr) ?? Path.GetFileName(fullPath);
                var isFolder = EverythingNative.IsFolderResult(index);
                var isFile = EverythingNative.IsFileResult(index);

                if (!isFile || !fullPath.EndsWith(".unitypackage", StringComparison.OrdinalIgnoreCase))
                    continue;

                long? size = EverythingNative.TryGetResultSize(index, out var rawSize) ? rawSize : null;
                DateTimeOffset? modified = EverythingNative.TryGetResultDateModified(index, out var fileTime)
                    ? DateTimeOffset.FromFileTime(fileTime)
                    : null;

                if (ShouldExcludeResult(fullPath, size))
                {
                    excludedCount++;
                    continue;
                }

                results.Add(new EverythingSearchResult(
                    name ?? fullPath,
                    fullPath,
                    isFolder,
                    isFile,
                    size,
                    modified));
            }

            return (results, excludedCount);
        }
        catch (ExternalException ex)
        {
            throw EverythingSearchException.NativeFailure(ex);
        }
        finally
        {
            EverythingNative.Reset();
        }
    }

    private static bool ShouldExcludeResult(string fullPath, long? sizeBytes)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
            return true;

        if (IsInExcludedLocation(fullPath))
            return true;

        if (sizeBytes is <= 0)
            return true;

        try
        {
            var fileInfo = new FileInfo(fullPath);
            if (!fileInfo.Exists || fileInfo.Length <= 0)
                return true;
        }
        catch (Exception)
        {
            return true;
        }

        return false;
    }

    private static bool IsInExcludedLocation(string fullPath)
    {
        var normalized = fullPath.Replace('/', '\\');

        foreach (var fragment in ExcludedPathFragments)
            if (normalized.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

        return false;
    }
}

public sealed class EverythingSearchException : Exception
{
    private EverythingSearchException(string message, EverythingErrorCode errorCode, Exception? inner = null)
        : base(message, inner)
    {
        ErrorCode = errorCode;
    }

    public EverythingErrorCode ErrorCode { get; }

    public static EverythingSearchException FromError(EverythingErrorCode errorCode)
    {
        return new EverythingSearchException(GetErrorMessage(errorCode), errorCode);
    }

    public static EverythingSearchException MissingLibrary(Exception inner)
    {
        return new EverythingSearchException(
            "Everything64.dll was not found. Place the Everything SDK DLL next to the EasyExtract executable.",
            EverythingErrorCode.LibraryMissing, inner);
    }

    public static EverythingSearchException MismatchedLibrary(Exception inner)
    {
        return new EverythingSearchException(
            "The Everything SDK DLL is incompatible with this application. Verify that Everything64.dll matches your OS architecture.",
            EverythingErrorCode.LibraryMismatch, inner);
    }

    public static EverythingSearchException NativeFailure(Exception inner)
    {
        return new EverythingSearchException("Everything search encountered an unexpected native error.",
            EverythingErrorCode.NativeFailure,
            inner);
    }

    private static string GetErrorMessage(EverythingErrorCode errorCode)
    {
        return errorCode switch
        {
            EverythingErrorCode.Ok => "Search completed successfully.",
            EverythingErrorCode.Memory => "Everything reported an out-of-memory condition while searching.",
            EverythingErrorCode.Ipc =>
                "Unable to reach Everything. Ensure Everything is running in the background and try again.",
            EverythingErrorCode.RegisterClassEx => "Everything failed to register its search window.",
            EverythingErrorCode.CreateWindow => "Everything could not create its search window.",
            EverythingErrorCode.CreateThread => "Everything could not create a helper thread.",
            EverythingErrorCode.InvalidIndex => "Everything reported an invalid result index.",
            EverythingErrorCode.InvalidCall => "Everything reported an invalid call sequence for the query.",
            EverythingErrorCode.LibraryMissing => "Everything SDK library missing.",
            EverythingErrorCode.LibraryMismatch => "Everything SDK library mismatch.",
            EverythingErrorCode.NativeFailure => "Unexpected native failure while querying Everything.",
            _ => $"Everything reported an unknown error ({(int)errorCode})."
        };
    }
}

public enum EverythingErrorCode
{
    Ok = 0,
    Memory = 1,
    Ipc = 2,
    RegisterClassEx = 3,
    CreateWindow = 4,
    CreateThread = 5,
    InvalidIndex = 6,
    InvalidCall = 7,
    LibraryMissing = 1000,
    LibraryMismatch = 1001,
    NativeFailure = 1002
}

internal static class EverythingNative
{
    // Request flags
    public const uint RequestFileName = 0x00000001;
    public const uint RequestPath = 0x00000002;
    public const uint RequestFullPathAndFileName = 0x00000004;
    public const uint RequestSize = 0x00000010;
    public const uint RequestDateModified = 0x00000040;
    public const uint RequestAttributes = 0x00000100;

    // Sort values
    public const uint SortNameAscending = 1;

    [DllImport("Everything64.dll", CharSet = CharSet.Unicode)]
    public static extern uint Everything_SetSearchW(string lpSearchString);

    [DllImport("Everything64.dll")]
    public static extern void Everything_SetMatchPath(bool bEnable);

    [DllImport("Everything64.dll")]
    public static extern void Everything_SetMatchCase(bool bEnable);

    [DllImport("Everything64.dll")]
    public static extern void Everything_SetMatchWholeWord(bool bEnable);

    [DllImport("Everything64.dll")]
    public static extern void Everything_SetRegex(bool bEnable);

    [DllImport("Everything64.dll")]
    public static extern void Everything_SetMax(uint dwMax);

    [DllImport("Everything64.dll")]
    public static extern void Everything_SetOffset(uint dwOffset);

    [DllImport("Everything64.dll")]
    public static extern void Everything_SetRequestFlags(uint dwRequestFlags);

    [DllImport("Everything64.dll")]
    public static extern void Everything_SetSort(uint dwSortType);

    [DllImport("Everything64.dll")]
    public static extern bool Everything_QueryW(bool bWait);

    [DllImport("Everything64.dll")]
    public static extern uint Everything_GetNumResults();

    [DllImport("Everything64.dll")]
    public static extern bool Everything_IsFolderResult(uint nIndex);

    [DllImport("Everything64.dll")]
    public static extern bool Everything_IsFileResult(uint nIndex);

    [DllImport("Everything64.dll", CharSet = CharSet.Unicode)]
    public static extern void Everything_GetResultFullPathName(uint nIndex, StringBuilder lpString, uint nMaxCount);

    [DllImport("Everything64.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr Everything_GetResultFileName(uint nIndex);

    [DllImport("Everything64.dll")]
    public static extern bool Everything_GetResultSize(uint nIndex, out long lpFileSize);

    [DllImport("Everything64.dll")]
    public static extern bool Everything_GetResultDateModified(uint nIndex, out long lpFileTime);

    [DllImport("Everything64.dll")]
    public static extern uint Everything_GetLastError();

    [DllImport("Everything64.dll")]
    public static extern void Everything_Reset();

    [DllImport("Everything64.dll")]
    public static extern bool Everything_IsDBLoaded();

    public static EverythingErrorCode GetLastError()
    {
        return (EverythingErrorCode)Everything_GetLastError();
    }

    public static bool Query(bool wait)
    {
        return Everything_QueryW(wait);
    }

    public static void SetSearch(string query)
    {
        Everything_SetSearchW(query);
    }

    public static void SetMax(uint max)
    {
        Everything_SetMax(max);
    }

    public static void SetOffset(uint offset)
    {
        Everything_SetOffset(offset);
    }

    public static void SetRequestFlags(uint flags)
    {
        Everything_SetRequestFlags(flags);
    }

    public static void SetSort(uint sort)
    {
        Everything_SetSort(sort);
    }

    public static void SetMatchPath(bool enable)
    {
        Everything_SetMatchPath(enable);
    }

    public static void SetRegex(bool enable)
    {
        Everything_SetRegex(enable);
    }

    public static void SetMatchCase(bool enable)
    {
        Everything_SetMatchCase(enable);
    }

    public static void SetMatchWholeWord(bool enable)
    {
        Everything_SetMatchWholeWord(enable);
    }

    public static uint GetNumResults()
    {
        return Everything_GetNumResults();
    }

    public static bool IsFolderResult(uint index)
    {
        return Everything_IsFolderResult(index);
    }

    public static bool IsFileResult(uint index)
    {
        return Everything_IsFileResult(index);
    }

    public static void GetResultFullPathName(uint index, StringBuilder builder, uint capacity)
    {
        Everything_GetResultFullPathName(index, builder, capacity);
    }

    public static IntPtr GetResultFileName(uint index)
    {
        return Everything_GetResultFileName(index);
    }

    public static bool IsDbLoaded()
    {
        return Everything_IsDBLoaded();
    }

    public static bool TryGetResultSize(uint index, out long size)
    {
        return Everything_GetResultSize(index, out size);
    }

    public static bool TryGetResultDateModified(uint index, out long fileTime)
    {
        return Everything_GetResultDateModified(index, out fileTime);
    }

    public static void Reset()
    {
        Everything_Reset();
    }
}