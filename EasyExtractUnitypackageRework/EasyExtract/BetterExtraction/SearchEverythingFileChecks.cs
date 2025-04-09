using EasyExtract.Services;

namespace EasyExtract.BetterExtraction;

public static class SearchEverythingFileChecks
{
    public static string GetFileSize(string filePath)
    {
        return !File.Exists(filePath)
            ? "File not found Failed to get file size"
            : new FileInfo(filePath).Length.ToString();
    }

    public static string GetFileDateTime(uint fileIndex, bool isCreationTime)
    {
        var path = Everything.GetResultFullPathName(fileIndex);
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;

        var fileInfo = new FileInfo(path);
        return isCreationTime
            ? fileInfo.CreationTime.ToString("dd-MM-yyyy")
            : fileInfo.LastWriteTime.ToString("dd-MM-yyyy");
    }
}