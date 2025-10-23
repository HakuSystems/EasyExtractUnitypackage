using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EasyExtractCrossPlatform.Models;

namespace EasyExtractCrossPlatform.Services;

public interface IEverythingSearchService
{
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

    private readonly SemaphoreSlim _queryGate = new(1, 1);

    public async Task<IReadOnlyList<EverythingSearchResult>> SearchAsync(
        string query,
        int maxResults,
        CancellationToken cancellationToken)
    {
        await EverythingSdkBootstrapper.EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(query))
            return Array.Empty<EverythingSearchResult>();

        maxResults = Math.Clamp(maxResults, 1, 2000);

        var entered = false;
        try
        {
            await _queryGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            entered = true;

            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return ExecuteSearchInternal(query, maxResults, cancellationToken);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (DllNotFoundException ex)
        {
            throw EverythingSearchException.MissingLibrary(ex);
        }
        catch (EntryPointNotFoundException ex)
        {
            throw EverythingSearchException.MismatchedLibrary(ex);
        }
        finally
        {
            if (entered)
                _queryGate.Release();
        }
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
    {
        await EverythingSdkBootstrapper.EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var entered = false;
        try
        {
            await _queryGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            entered = true;

            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return EverythingNative.IsDbLoaded();
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (DllNotFoundException ex)
        {
            throw EverythingSearchException.MissingLibrary(ex);
        }
        catch (EntryPointNotFoundException ex)
        {
            throw EverythingSearchException.MismatchedLibrary(ex);
        }
        finally
        {
            if (entered)
                _queryGate.Release();
        }
    }

    private static IReadOnlyList<EverythingSearchResult> ExecuteSearchInternal(
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
            var unityFilter = "ext:unitypackage";
            var composedQuery = string.IsNullOrWhiteSpace(trimmedQuery)
                ? unityFilter
                : $"{trimmedQuery} {unityFilter}";

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

                results.Add(new EverythingSearchResult(
                    name ?? fullPath,
                    fullPath,
                    isFolder,
                    isFile,
                    size,
                    modified));
            }

            return results;
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