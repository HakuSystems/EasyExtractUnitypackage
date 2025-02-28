using EasyExtract.Config;

namespace EasyExtract.BetterExtraction;

public class LocateUnitypackage
{
    private OpenFileDialog OpenUnitypackageDialog { get; } = new()
    {
        Filter = "Unitypackage files (*.unitypackage)|*.unitypackage",
        Title = "Select a Unitypackage file",
        Multiselect = true
    };

    private HashChecks HashChecks { get; } = new();
    private FilterQueue FilterQueue { get; } = new();

    public async Task<List<UnitypackageFileInfo>?> LocateUnitypackageFilesAsync()
    {
        if (OpenUnitypackageDialog.ShowDialog() != true)
            return null;

        var fileInfos = OpenUnitypackageDialog.FileNames
            .Select(file => new FileInfo(file))
            .ToList();

        var fileDetails = await Task.Run(() => fileInfos.Select(file =>
        {
            var hash = HashChecks.ComputeFileHash(file);
            return new UnitypackageFileInfo
            {
                FileName = file.Name,
                FileHash = hash,
                FileSize = file.Length.ToString(), // File size in bytes
                FileDate = file.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"),
                FilePath = file.FullName,
                FileExtension = file.Extension,
                IsInQueue = true // Mark file as currently in queue
            };
        }).ToList());

        ConfigHandler.Instance.Config.UnitypackageFiles.AddRange(fileDetails);
        FilterQueue.FilterDuplicates();

        return fileDetails;
    }
}