using System.IO;
using System.IO.Compression;
using EasyExtract.Config;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace EasyExtract.Extraction;

public class ExtractionHandler
{
    private static string LastExtractedPath { get; } = ConfigModel.LastExtractedPath;
    private static string EasyExtractTempPath { get; } = ConfigModel.DefaultTempPath;

    public async Task<bool> ExtractUnitypackage(SearchEverythingModel unitypackage)
    {
        try
        {
            var tempFolder = GetTempFolderPath(unitypackage);
            var targetFolder = GetTargetFolderPath(unitypackage);

            CreateDirectories(tempFolder, targetFolder);

            await ExtractAndWriteFiles(unitypackage, tempFolder);
            MoveFilesFromTempToTargetFolder(tempFolder, targetFolder);

            Directory.Delete(tempFolder, true);

            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error while extracting unitypackage: {e.Message}");
            return false;
        }
    }

    public string GetTempFolderPath(SearchEverythingModel unitypackage)
    {
        var tempFolder = Path.Combine(EasyExtractTempPath, unitypackage.UnityPackageName);
        DeleteIfDirectoryExists(tempFolder);
        return tempFolder;
    }

    public string GetTargetFolderPath(SearchEverythingModel unitypackage)
    {
        var targetFolder = Path.Combine(LastExtractedPath, unitypackage.UnityPackageName);
        DeleteIfDirectoryExists(targetFolder);
        return targetFolder;
    }

    public void DeleteIfDirectoryExists(string directory)
    {
        if (Directory.Exists(directory))
            Directory.Delete(directory, true);
    }

    public void CreateDirectories(params string[] directories)
    {
        foreach (var dir in directories)
            Directory.CreateDirectory(dir);
    }

    public async Task ExtractAndWriteFiles(SearchEverythingModel unitypackage, string tempFolder)
    {
        await Task.Run(() =>
        {
            using Stream inStream = File.OpenRead(unitypackage.UnityPackagePath);
            using Stream gzipStream = new GZipStream(inStream, CompressionMode.Decompress);
            using var memoryStream = new MemoryStream();
            gzipStream.CopyTo(memoryStream);
            memoryStream.Position = 0;
            using var archive = ArchiveFactory.Open(memoryStream);

            foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
            {
                var extractionOptions = new ExtractionOptions
                {
                    ExtractFullPath = true,
                    Overwrite = true
                };
                entry.WriteToDirectory(tempFolder, extractionOptions);
            }
        });
    }

    public void MoveFilesFromTempToTargetFolder(string tempFolder, string targetFolder)
    {
        foreach (var d in Directory.EnumerateDirectories(tempFolder))
        {
            var targetFullPath = string.Empty;
            var targetFullFile = string.Empty;
            try
            {
                if (File.Exists(Path.Combine(d, "pathname")))
                {
                    var hashPathName = File.ReadAllText(Path.Combine(d, "pathname"));
                    targetFullPath = Path.GetDirectoryName(Path.Combine(targetFolder, hashPathName));
                    targetFullFile = Path.Combine(targetFolder, hashPathName);
                }

                MoveFileIfExists(d, "asset", targetFullPath, targetFullFile);
                MoveFileIfExists(d, "asset.meta", targetFullPath, targetFullFile + ".meta");
                MoveFileIfExists(d, "preview.png", targetFullPath, targetFullFile + ".EASYEXTRACTPREVIEW.png");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while moving file: {ex.Message}");
            }
        }
    }

    public void MoveFileIfExists(string directory, string fileName, string targetFullPath,
        string targetFullFile)
    {
        if (File.Exists(Path.Combine(directory, fileName)))
        {
            Directory.CreateDirectory(targetFullPath);
            File.Move(Path.Combine(directory, fileName), targetFullFile);
        }
    }
}