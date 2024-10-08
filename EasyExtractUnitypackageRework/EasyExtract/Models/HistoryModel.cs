namespace EasyExtract.Config;

public class HistoryModel
{
    public string FileName { get; set; } = string.Empty;
    public string ExtractedPath { get; set; } = string.Empty;
    public DateTime ExtractedDate { get; set; } = DateTime.Now;
    public int TotalFiles { get; set; } = 0;
}