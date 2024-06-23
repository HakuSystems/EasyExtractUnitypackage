using System.IO;
using System.IO.Compression;
using EasyExtract.Config;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace EasyExtract.Extraction;

public class ExtractionHandler
{
    private readonly BetterLogger _logger = new();
    private readonly ConfigHelper ConfigHelper = new();

    public async Task<bool> ExtractUnitypackage(SearchEverythingModel unitypackage)
    {
        try
        {
            var tempFolder = GetTempFolderPath(unitypackage);
            var targetFolder = GetTargetFolderPath(unitypackage);

            CreateDirectories(await tempFolder, await targetFolder);

            await ExtractAndWriteFiles(unitypackage, await tempFolder);
            MoveFilesFromTempToTargetFolder(await tempFolder, await targetFolder);

            Directory.Delete(await tempFolder, true);

            await _logger.LogAsync($"Successfully extracted {unitypackage.UnityPackageName}", "ExtractionHandler.cs",
                Importance.Info); // Log successful extraction
            return true;
        }
        catch (Exception e)
        {
            await _logger.LogAsync($"Error while extracting unitypackage: {e.Message}", "ExtractionHandler.cs",
                Importance.Error); // Log extraction error
            return false;
        }
    }

    private async Task<string> GetTempFolderPath(SearchEverythingModel unitypackage)
    {
        var tempFolder = Path.Combine(ConfigHelper.Config.DefaultTempPath, unitypackage.UnityPackageName);
        DeleteIfDirectoryExists(tempFolder);
        await _logger.LogAsync($"Temporary folder path set to: {tempFolder}", "ExtractionHandler.cs",
            Importance.Info); // Log temp folder path
        return tempFolder;
    }

    private async Task<string> GetTargetFolderPath(SearchEverythingModel unitypackage)
    {
        var targetFolder = Path.Combine(ConfigHelper.Config.LastExtractedPath, unitypackage.UnityPackageName);
        DeleteIfDirectoryExists(targetFolder);
        await _logger.LogAsync($"Target folder path set to: {targetFolder}", "ExtractionHandler.cs",
            Importance.Info); // Log target folder path
        return targetFolder;
    }

    private async void DeleteIfDirectoryExists(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
            await _logger.LogAsync($"Deleted existing directory: {directory}", "ExtractionHandler.cs",
                Importance.Warning); // Log directory deletion
        }
    }

    private async void CreateDirectories(params string[] directories)
    {
        foreach (var dir in directories)
        {
            Directory.CreateDirectory(dir);
            await _logger.LogAsync($"Created directory: {dir}", "ExtractionHandler.cs",
                Importance.Info); // Log directory creation
        }
    }

    private async Task ExtractAndWriteFiles(SearchEverythingModel unitypackage, string tempFolder)
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
        await _logger.LogAsync($"Extracted and wrote files for {unitypackage.UnityPackageName} to temporary folder",
            "ExtractionHandler.cs", Importance.Info); // Log extraction
    }

    private async void MoveFilesFromTempToTargetFolder(string tempFolder, string targetFolder)
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
                await _logger.LogAsync($"Error while moving file from {d} to {targetFullFile}: {ex.Message}",
                    "ExtractionHandler.cs", Importance.Error); // Log move file error
            }
        }

        await _logger.LogAsync($"Moved files from temporary folder to target folder: {targetFolder}",
            "ExtractionHandler.cs", Importance.Info); // Log move files
    }

    private async void MoveFileIfExists(string directory, string fileName, string targetFullPath, string targetFullFile)
    {
        if (File.Exists(Path.Combine(directory, fileName)))
        {
            Directory.CreateDirectory(targetFullPath);
            File.Move(Path.Combine(directory, fileName), targetFullFile);
            await _logger.LogAsync($"Moved file {fileName} to {targetFullFile}", "ExtractionHandler.cs",
                Importance.Info); // Log move file
        }
    }
}