namespace EasyExtract.CustomDesign;

public class BackgroundModel
{
    public string BackgroundPath { get; set; }
    public string? DefaultBackgroundResource { get; set; } = "{DynamicResource BackgroundPrimaryBrush}";
    public double BackgroundOpacity { get; set; } = 0.5;
}