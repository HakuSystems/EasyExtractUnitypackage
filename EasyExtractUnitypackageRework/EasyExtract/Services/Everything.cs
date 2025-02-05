using System.Text;

namespace EasyExtract.Services;

public static class Everything
{
    public const int RequestFileName = 0x00000001;
    public const int RequestPath = 0x00000002;

    /// <summary>
    ///     Returns the full path name for a given result index.
    /// </summary>
    public static string GetResultFullPathName(uint index)
    {
        const int capacity = 260;
        var sb = new StringBuilder(capacity);
        GetResultFullPathNameNative(index, sb, capacity);
        return sb.ToString();
    }

    [DllImport("Everything64.dll", CharSet = CharSet.Unicode)]
    public static extern uint Everything_SetSearchW(string searchString);

    [DllImport("Everything64.dll", CharSet = CharSet.Unicode)]
    public static extern bool Everything_QueryW(bool wait);

    [DllImport("Everything64.dll")]
    public static extern uint Everything_GetNumResults();

    [DllImport("Everything64.dll", CharSet = CharSet.Unicode, EntryPoint = "Everything_GetResultFullPathName")]
    private static extern void GetResultFullPathNameNative(uint index, StringBuilder lpString, uint nMaxCount);


    [DllImport("Everything64.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr Everything_GetResultFileName(uint index);

    [DllImport("Everything64.dll")]
    public static extern void Everything_SetRequestFlags(uint requestFlags);
}