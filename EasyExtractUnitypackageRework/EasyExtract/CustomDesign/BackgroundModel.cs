namespace EasyExtract.CustomDesign;

public class BackgroundModel
{
    public string BackgroundPath { get; set; } = string.Empty;
    public string? DefaultBackgroundResource { get; set; } = null;
    public double BackgroundOpacity { get; set; } = 0.5;
}