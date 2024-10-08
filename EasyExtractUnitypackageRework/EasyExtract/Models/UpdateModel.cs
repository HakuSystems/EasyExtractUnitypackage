namespace EasyExtract.Config;

public class UpdateModel
{
    public readonly string RepoName = "EasyExtractUnitypackage";
    public readonly string RepoOwner = "HakuSystems";
    public bool AutoUpdate { get; set; } = true;
    public string CurrentVersion { get; set; } = $"V{Assembly.GetExecutingAssembly().GetName().Version}";
}