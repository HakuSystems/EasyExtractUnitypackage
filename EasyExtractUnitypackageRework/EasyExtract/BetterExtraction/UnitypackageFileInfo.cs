namespace EasyExtract.BetterExtraction;

public class UnitypackageFileInfo
{
    public string? FileName { get; set; }
    public string FileHash { get; set; }
    public string? FileSize { get; set; }
    public string? FileDate { get; set; }
    public string? FilePath { get; set; }
    public string? FileExtension { get; set; }
    public bool IsInQueue { get; set; } = true; // default value
}