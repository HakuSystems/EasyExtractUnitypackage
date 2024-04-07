namespace EasyExtract.Config;

public class HistoryModel
{
    public string FileName { get; set; }
    public string ExtractedPath { get; set; }
    public DateTime ExtractedDate { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();
}