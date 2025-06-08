namespace EasyExtract.BetterExtraction;

public class UnitypackageFileInfo
{
    public string? FileName { get; init; }
    public string? FileHash { get; init; } // Legacy MD5 hash for backward compatibility
    public string? FileSize { get; init; }
    public string? FileDate { get; init; }
    public string? FilePath { get; init; }
    public string? FileExtension { get; init; }
    public bool IsInQueue { get; set; } = true; // default value
    public bool IsExtracting { get; set; } // default value
    public FileHashInfo? HashInfo { get; init; } // New property for detailed hash information
    
    /// <summary>
    /// Creates a new UnitypackageFileInfo with multiple hash values
    /// </summary>
    /// <param name="file">The file information</param>
    /// <param name="hashInfo">The hash information</param>
    /// <returns>A new UnitypackageFileInfo instance</returns>
    public static UnitypackageFileInfo CreateWithMultipleHashes(FileInfo file, FileHashInfo hashInfo)
    {
        return new UnitypackageFileInfo
        {
            FileName = file.Name,
            FileHash = hashInfo.MD5Hash, // For backward compatibility
            FileSize = file.Length.ToString(),
            FileDate = file.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"),
            FilePath = file.FullName,
            FileExtension = file.Extension,
            HashInfo = hashInfo
        };
    }
}
