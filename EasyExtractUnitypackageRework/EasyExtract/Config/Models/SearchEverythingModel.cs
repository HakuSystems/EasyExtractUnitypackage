namespace EasyExtract.Config.Models;

public class SearchEverythingModel
{
    public string UnityPackageName { get; set; } = string.Empty;
    public string UnityPackagePath { get; set; } = string.Empty;
    public uint Id { get; set; } = 0;

    public string ModifiedTime { get; set; } = string.Empty;
    public string CreatedTime { get; set; } = string.Empty;
}