using System;
using System.Runtime.InteropServices;
using System.Text;

namespace EasyExtractUnitypackageRework.Methods;

public class Everything
{
    public static IntPtr Everything_GetResultFullPathName(uint nIndex)
    {
        //return as string
        var sb = new StringBuilder(260);
        Everything_GetResultFullPathName(nIndex, sb, 260);
        return Marshal.StringToHGlobalUni(sb.ToString());
    }

    #region publicants

    public const int EVERYTHING_REQUEST_FILE_NAME = 0x00000001;
    public const int EVERYTHING_REQUEST_PATH = 0x00000002;

    [DllImport("Everything64.dll", CharSet = CharSet.Unicode)]
    public static extern uint Everything_SetSearchW(string lpSearchString);

    [DllImport("Everything64.dll")]
    public static extern bool Everything_QueryW(bool bWait);

    [DllImport("Everything64.dll")]
    public static extern void Everything_SortResultsByPath();

    [DllImport("Everything64.dll")]
    public static extern uint Everything_GetNumResults();

    [DllImport("Everything64.dll")]
    public static extern void Everything_Reset();

    [DllImport("Everything64.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr Everything_GetResultFileName(uint nIndex);

    // Everything 1.4
    [DllImport("Everything64.dll")]
    public static extern void Everything_SetSort(uint dwSortType);

    [DllImport("Everything64.dll")]
    public static extern void Everything_SetRequestFlags(uint dwRequestFlags);

    [DllImport("Everything64.dll", CharSet = CharSet.Unicode)]
    private static extern void Everything_GetResultFullPathName(uint nIndex, StringBuilder lpString, uint nMaxCount);

    #endregion
}