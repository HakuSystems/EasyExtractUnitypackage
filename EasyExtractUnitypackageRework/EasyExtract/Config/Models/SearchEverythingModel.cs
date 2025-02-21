namespace EasyExtract.Config.Models;

public class SearchEverythingModel
{
    public string? FileName { get; set; }
    public string? FilePath { get; set; }
    public string? FileSize { get; set; }
    public string? ModifiedTime { get; set; }
    public string? CreatedTime { get; set; }
    public uint Id { get; set; }
}