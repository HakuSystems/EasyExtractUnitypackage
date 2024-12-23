using System.Globalization;
using System.IO.Compression;
using EasyExtract.Config;
using EasyExtract.Models;
using EasyExtract.Utilities;

namespace EasyExtract.Services;

public class ExtractionHandler
{
    private readonly ConfigHelper _configHelper = new();

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
            await BetterLogger.LogAsync($"Temporary folder path: {tempFolder}", Importance.Info);
            await BetterLogger.LogAsync($"Target folder path: {targetFolder}", Importance.Info);

            await CreateDirectories(tempFolder, targetFolder);

            await ExtractAndWriteFiles(unitypackage, tempFolder);
            await MoveFilesFromTempToTargetFolder(tempFolder, targetFolder);

            Directory.Delete(tempFolder, true);

            await BetterLogger.LogAsync($"Successfully extracted {unitypackage.UnityPackageName}",
                Importance.Info);
            return true;
        }
        catch (Exception e)
        {
            await BetterLogger.LogAsync($"Error while extracting unitypackage: {e.Message}",
                Importance.Error);
            return false;
        }
    }

    private async Task<string> GetTempFolderPath(SearchEverythingModel unitypackage)
    {
        if (unitypackage.UnityPackageName != null)
        {
            var tempFolder = Path.Combine(_configHelper.Config.DefaultTempPath, unitypackage.UnityPackageName);
            await DeleteIfDirectoryExists(tempFolder);
            await BetterLogger.LogAsync($"Temporary folder path set to: {tempFolder}",
                Importance.Info);
            return tempFolder;
        }

        return string.Empty;
    }

    private async Task<string> GetTargetFolderPath(SearchEverythingModel unitypackage)
    {
        if (unitypackage.UnityPackageName != null)
        {
            var targetFolder = Path.Combine(_configHelper.Config.LastExtractedPath, unitypackage.UnityPackageName);
            await DeleteIfDirectoryExists(targetFolder);
            await BetterLogger.LogAsync($"Target folder path set to: {targetFolder}",
                Importance.Info);
            return targetFolder;
        }

        return string.Empty;
    }

    private static async Task DeleteIfDirectoryExists(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
            await BetterLogger.LogAsync($"Deleted existing directory: {directory}",
                Importance.Warning);
        }
    }

    private static async Task CreateDirectories(params string[] directories)
    {
        foreach (var dir in directories)
        {
            // Path validation
            if (dir.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            {
                await BetterLogger.LogAsync($"Invalid directory path: {dir}", Importance.Error);
                throw new InvalidOperationException($"Invalid directory path: {dir}");
            }

            Directory.CreateDirectory(dir);
            await BetterLogger.LogAsync($"Created directory: {dir}", Importance.Info);
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
                if (entry.Key != null)
                {
                    var filePath = Path.Combine(tempFolder, entry.Key);

                    // Creates all directories and subdirectories as specified by filePath.
                    Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? string.Empty);

                    entry.WriteToFile(filePath, new ExtractionOptions
                    {
                        Overwrite = true
                    });
                }
        });

        await BetterLogger.LogAsync($"Extracted and wrote files for {unitypackage.UnityPackageName} to temporary folder",
            Importance.Info);
    }

    private static async Task MoveFilesFromTempToTargetFolder(string tempFolder, string targetFolder)
    {
        foreach (var d in Directory.EnumerateDirectories(tempFolder))
        {
            var targetFullPath = string.Empty;
            var targetFullFile = string.Empty;
            try
            {
                if (File.Exists(Path.Combine(d, "pathname")))
                {
                    var hashPathName = await File.ReadAllTextAsync(Path.Combine(d, "pathname"));
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

                if (targetFullPath != null)
                {
                    await MoveFileIfExists(d, "asset", targetFullPath, targetFullFile);
                    await MoveFileIfExists(d, "asset.meta", targetFullPath, targetFullFile + ".meta");
                    await MoveFileIfExists(d, "preview.png", targetFullPath, targetFullFile + ".EASYEXTRACTPREVIEW.png");
                }
            }
            catch (Exception ex)
            {
                await BetterLogger.LogAsync($"Error while moving file from {d} to {targetFullFile}: {ex.Message}",
                    Importance.Error);
            }
        }

        await BetterLogger.LogAsync($"Moved files from temporary folder to target folder: {targetFolder}",
            Importance.Info);
    }

    private static async Task MoveFileIfExists(string directory, string fileName, string targetFullPath, string targetFullFile)
    {
        var sourceFilePath = Path.Combine(directory, fileName);
        if (!File.Exists(sourceFilePath)) return;

        try
        {
            Directory.CreateDirectory(targetFullPath);
            File.Move(sourceFilePath, targetFullFile, true);
        }
        catch (IOException ioEx)
        {
            await BetterLogger.LogAsync($"I/O error while moving file {sourceFilePath} to {targetFullFile}: {ioEx.Message}",
                Importance.Error);
        }
        catch (UnauthorizedAccessException uaEx)
        {
            await BetterLogger.LogAsync(
                $"Access denied while moving file {sourceFilePath} to {targetFullFile}: {uaEx.Message}",
                Importance.Error);
        }
        catch (Exception ex)
        {
            await BetterLogger.LogAsync(
                $"Unexpected error while moving file {sourceFilePath} to {targetFullFile}: {ex.Message}",
                Importance.Error);
        }
    }
}