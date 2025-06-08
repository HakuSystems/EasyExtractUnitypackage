namespace EasyExtract.Utilities.Logger;

public class PerformanceMetrics
{
    public long TotalMemory { get; set; }
    public long UsedMemory { get; set; }
    public double CpuUsage { get; set; }
    public int ThreadCount { get; set; }
    public long GcCollections { get; set; }
    public TimeSpan Uptime { get; set; }
}