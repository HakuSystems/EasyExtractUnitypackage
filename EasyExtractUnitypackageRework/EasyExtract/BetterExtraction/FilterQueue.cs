using EasyExtract.Config;

namespace EasyExtract.BetterExtraction;

public class FilterQueue
{
    public void FilterDuplicates()
    {
        var uniqueFiles = ConfigHandler.Instance.Config.UnitypackageFiles
            .GroupBy(file => new
            {
                file.FileHash, file.FileName, file.FileSize, file.FileDate, file.FilePath, file.FileExtension
            })
            .Select(group => group.First())
            .ToList();


        ConfigHandler.Instance.Config.UnitypackageFiles = uniqueFiles;
    }
}