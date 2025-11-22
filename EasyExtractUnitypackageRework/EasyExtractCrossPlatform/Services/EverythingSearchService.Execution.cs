using System.Runtime.InteropServices;
using System.Text;

namespace EasyExtractCrossPlatform.Services;

public sealed partial class EverythingSearchService
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