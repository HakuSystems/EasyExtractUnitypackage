namespace EasyExtract.BetterExtraction;

public class UnitypackageFileInfo
{
    public string? FileName { get; init; }
    public string? FileHash { get; init; }
    public string? FileSize { get; init; }
    public string? FileDate { get; init; }
    public string? FilePath { get; init; }
    public string? FileExtension { get; init; }
    public bool IsInQueue { get; set; } = true; // default value
    public bool IsExtracting { get; set; } // default value
}