namespace EasyExtract.Core.Utilities;

public static class FileLockHelper
{
    /// <summary>
    ///     True when the IOException is a Windows sharing or lock violation,
    ///     i.e. another process currently holds the file.
    /// </summary>
    public static bool IsFileLockContention(IOException ex)
    {
        const int sharingViolation = unchecked((int)0x80070020);
        const int lockViolation = unchecked((int)0x80070021);
        return ex.HResult == sharingViolation || ex.HResult == lockViolation;
    }
}
