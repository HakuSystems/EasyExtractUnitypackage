using System.Security.Cryptography;
using System.Text;
using EasyExtract.Config;

namespace EasyExtract.BetterExtraction;

public class LocateUnitypackage
{
    public LocateUnitypackage()
    {
        OpenUnitypackageDialog = new OpenFileDialog
        {
            Filter = "Unitypackage files (*.unitypackage)|*.unitypackage",
            Title = "Select a Unitypackage file",
            Multiselect = true
        };
    }

    private OpenFileDialog OpenUnitypackageDialog { get; }
    private FilterQueue FilterQueue { get; } = new();

    public List<UnitypackageFileInfo>? LocateUnitypackageFiles()
    {
        if (OpenUnitypackageDialog.ShowDialog() != true)
            return null;

        var fileInfos = OpenUnitypackageDialog.FileNames
            .Select(file => new FileInfo(file))
            .ToList();

        var fileDetails = fileInfos.Select(file =>
        {
            var hash = ComputeFileHash(file);
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
        }).ToList();

        ConfigHandler.Instance.Config.UnitypackageFiles.AddRange(fileDetails);
        FilterQueue.FilterDuplicates();

        return fileDetails;
    }

    private string ComputeFileHash(FileInfo file)
    {
        using var stream = file.OpenRead();
        using var md5 = MD5.Create();
        var hashBytes = md5.ComputeHash(stream);
        var sb = new StringBuilder();
        foreach (var b in hashBytes)
            sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}