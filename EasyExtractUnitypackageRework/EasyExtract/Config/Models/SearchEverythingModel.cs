namespace EasyExtract.Config.Models;

public class SearchEverythingModel
{
    public string? FileName { get; init; }
    public string? FilePath { get; init; }
    public string? FileSize { get; init; }
    public string? ModifiedTime { get; init; }
    public string? CreatedTime { get; init; }
}