namespace EasyExtractCrossPlatform.Models;

public sealed class UnityPackageFile
{
    public string FileName { get; set; } = string.Empty;
    public string FileHash { get; set; } = string.Empty;
    public string FileSize { get; set; } = string.Empty;
    public string FileDate { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string FileExtension { get; set; } = string.Empty;
    public bool IsInQueue { get; set; }
    public bool IsExtracting { get; set; }
}