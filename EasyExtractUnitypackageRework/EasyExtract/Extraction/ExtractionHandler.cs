using System.Globalization;
using System.IO;
using System.IO.Compression;
using EasyExtract.Config;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace EasyExtract.Extraction;

public class ExtractionHandler
{
    private readonly ConfigHelper _configHelper = new();
    private readonly BetterLogger _logger = new();

    public async Task ExtractUnitypackageFromContextMenu(string path)
    {
        var unitypackage = new SearchEverythingModel
        {
            UnityPackageName = Path.GetFileNameWithoutExtension(path),
            UnityPackagePath = path
        };

        await ExtractUnitypackage(unitypackage);
    }

    public async Task<bool> ExtractUnitypackage(SearchEverythingModel unitypackage)
    {
        try
        {
            var tempFolder = await GetTempFolderPath(unitypackage);
            var targetFolder = await GetTargetFolderPath(unitypackage);

            // Debug logs for paths
            await _logger.LogAsync($"Temporary folder path: {tempFolder}", "ExtractionHandler.cs", Importance.Info);
            await _logger.LogAsync($"Target folder path: {targetFolder}", "ExtractionHandler.cs", Importance.Info);

            await CreateDirectories(tempFolder, targetFolder);

            await ExtractAndWriteFiles(unitypackage, tempFolder);
            await MoveFilesFromTempToTargetFolder(tempFolder, targetFolder);

            Directory.Delete(tempFolder, true);

            await _logger.LogAsync($"Successfully extracted {unitypackage.UnityPackageName}", "ExtractionHandler.cs",
                Importance.Info);
            return true;
        }
        catch (Exception e)
        {
            await _logger.LogAsync($"Error while extracting unitypackage: {e.Message}", "ExtractionHandler.cs",
                Importance.Error);
            return false;
        }
    }

    private async Task<string> GetTempFolderPath(SearchEverythingModel unitypackage)
    {
        var tempFolder = Path.Combine(_configHelper.Config.DefaultTempPath, unitypackage.UnityPackageName);
        await DeleteIfDirectoryExists(tempFolder);
        await _logger.LogAsync($"Temporary folder path set to: {tempFolder}", "ExtractionHandler.cs", Importance.Info);
        return tempFolder;
    }

    private async Task<string> GetTargetFolderPath(SearchEverythingModel unitypackage)
    {
        var targetFolder = Path.Combine(_configHelper.Config.LastExtractedPath, unitypackage.UnityPackageName);
        await DeleteIfDirectoryExists(targetFolder);
        await _logger.LogAsync($"Target folder path set to: {targetFolder}", "ExtractionHandler.cs", Importance.Info);
        return targetFolder;
    }

    private async Task DeleteIfDirectoryExists(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
            await _logger.LogAsync($"Deleted existing directory: {directory}", "ExtractionHandler.cs",
                Importance.Warning);
        }
    }

    private async Task CreateDirectories(params string[] directories)
    {
        foreach (var dir in directories)
        {
            // Path validation
            if (dir.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            {
                await _logger.LogAsync($"Invalid directory path: {dir}", "ExtractionHandler.cs", Importance.Error);
                throw new InvalidOperationException($"Invalid directory path: {dir}");
            }

            Directory.CreateDirectory(dir);
            await _logger.LogAsync($"Created directory: {dir}", "ExtractionHandler.cs", Importance.Info);
        }
    }

    private async Task ExtractAndWriteFiles(SearchEverythingModel unitypackage, string tempFolder)
    {
        await Task.Run(() =>
        {
            using var inStream = File.OpenRead(unitypackage.UnityPackagePath);
            using var gzipStream = new GZipStream(inStream, CompressionMode.Decompress);
            using var memoryStream = new MemoryStream();
            gzipStream.CopyTo(memoryStream);
            memoryStream.Position = 0;
            using var archive = ArchiveFactory.Open(memoryStream);

            foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
            {
                var filePath = Path.Combine(tempFolder, entry.Key);

                // Creates all directories and subdirectories as specified by filePath.
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));

                entry.WriteToFile(filePath, new ExtractionOptions
                {
                    Overwrite = true
                });
            }
        });

        await _logger.LogAsync($"Extracted and wrote files for {unitypackage.UnityPackageName} to temporary folder",
            "ExtractionHandler.cs", Importance.Info);
    }

    private async Task MoveFilesFromTempToTargetFolder(string tempFolder, string targetFolder)
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
                    if (hashPathName.Any(c =>
                            char.GetUnicodeCategory(c) == UnicodeCategory.Format ||
                            char.GetUnicodeCategory(c) == UnicodeCategory.Control))
                        hashPathName = new string(hashPathName.Where(c =>
                            char.GetUnicodeCategory(c) != UnicodeCategory.Format &&
                            char.GetUnicodeCategory(c) != UnicodeCategory.Control).ToArray());

                    // remove additional 00 at the end of the file extension.
                    if (hashPathName.EndsWith("00")) hashPathName = hashPathName.Substring(0, hashPathName.Length - 2);

                    targetFullPath = Path.GetDirectoryName(Path.Combine(targetFolder, hashPathName));
                    targetFullFile = Path.Combine(targetFolder, hashPathName);
                }

                await MoveFileIfExists(d, "asset", targetFullPath, targetFullFile);
                await MoveFileIfExists(d, "asset.meta", targetFullPath, targetFullFile + ".meta");
                await MoveFileIfExists(d, "preview.png", targetFullPath, targetFullFile + ".EASYEXTRACTPREVIEW.png");
            }
            catch (Exception ex)
            {
                await _logger.LogAsync($"Error while moving file from {d} to {targetFullFile}: {ex.Message}",
                    "ExtractionHandler.cs", Importance.Error);
            }
        }

        await _logger.LogAsync($"Moved files from temporary folder to target folder: {targetFolder}",
            "ExtractionHandler.cs", Importance.Info);
    }

    private async Task MoveFileIfExists(string directory, string fileName, string targetFullPath, string targetFullFile)
    {
        var sourceFilePath = Path.Combine(directory, fileName);
        if (!File.Exists(sourceFilePath)) return;

        try
        {
            Directory.CreateDirectory(targetFullPath);
            File.Move(sourceFilePath, targetFullFile, true);
            await _logger.LogAsync($"Moved file {fileName} to {targetFullFile}", "ExtractionHandler.cs",
                Importance.Info);
        }
        catch (IOException ioEx)
        {
            await _logger.LogAsync($"I/O error while moving file {sourceFilePath} to {targetFullFile}: {ioEx.Message}",
                "ExtractionHandler.cs", Importance.Error);
        }
        catch (UnauthorizedAccessException uaEx)
        {
            await _logger.LogAsync(
                $"Access denied while moving file {sourceFilePath} to {targetFullFile}: {uaEx.Message}",
                "ExtractionHandler.cs", Importance.Error);
        }
        catch (Exception ex)
        {
            await _logger.LogAsync(
                $"Unexpected error while moving file {sourceFilePath} to {targetFullFile}: {ex.Message}",
                "ExtractionHandler.cs", Importance.Error);
        }
    }
}