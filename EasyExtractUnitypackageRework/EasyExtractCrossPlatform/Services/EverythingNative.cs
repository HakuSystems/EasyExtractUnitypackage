using System;
using System.Runtime.InteropServices;
using System.Text;

namespace EasyExtractCrossPlatform.Services;

internal static class EverythingNative
{
    public const uint RequestFileName = 0x00000001;
    public const uint RequestPath = 0x00000002;
    public const uint RequestFullPathAndFileName = 0x00000004;
    public const uint RequestSize = 0x00000010;
    public const uint RequestDateModified = 0x00000040;
    public const uint RequestAttributes = 0x00000100;

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
