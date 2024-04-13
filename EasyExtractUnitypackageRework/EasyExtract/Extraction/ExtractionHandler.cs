using EasyExtract.Config;

namespace EasyExtract.Extraction;

public class ExtractionHandler
{
    private static string LastExtractedPath { get; } = ConfigModel.LastExtractedPath;

    public static async Task<bool> ExtractUnitypackage(SearchEverythingModel unitypackage)
    {
        var historyItem = new HistoryModel
        {
            FileName = unitypackage.UnityPackageName,
            ExtractedPath = LastExtractedPath, //temporarily
            ExtractedDate = DateTime.Now
        };
        await ConfigHelper.AddToHistory(historyItem);
        //todo: implement extraction
        return true;
    }
}