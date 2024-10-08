using System.Text;

namespace EasyExtract.Services;

public class Everything
{
    public const int EVERYTHING_REQUEST_FILE_NAME = 0x00000001;
    public const int EVERYTHING_REQUEST_PATH = 0x00000002;

    public static IntPtr Everything_GetResultFullPathName(uint nIndex)
    {
        //return as string
        var sb = new StringBuilder(260);
        Everything_GetResultFullPathName(nIndex, sb, 260);
        return Marshal.StringToHGlobalUni(sb.ToString());
    }


    [DllImport("Everything64.dll", CharSet = CharSet.Unicode)]
    public static extern uint Everything_SetSearchW(string lpSearchString);


    [DllImport("Everything64.dll")]
    public static extern bool Everything_QueryW(bool bWait);


    [DllImport("Everything64.dll")]
    public static extern uint Everything_GetNumResults();


    [DllImport("Everything64.dll", CharSet = CharSet.Unicode)]
    private static extern void Everything_GetResultFullPathName(uint nIndex, StringBuilder lpString, uint nMaxCount);


    [DllImport("Everything64.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr Everything_GetResultFileName(uint nIndex);


    [DllImport("Everything64.dll")]
    public static extern void Everything_SetRequestFlags(uint dwRequestFlags);
}