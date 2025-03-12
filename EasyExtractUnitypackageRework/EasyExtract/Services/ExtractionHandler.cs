using System.Globalization;
using System.IO.Compression;
using EasyExtract.Config;
using EasyExtract.Config.Models;
using EasyExtract.Utilities;
using SharpCompress.Readers.Tar;

namespace EasyExtract.Services;

public class ExtractionHandler
{
    public async Task ExtractUnitypackageFromContextMenu(string path)
    {
        var unitypackage = new SearchEverythingModel
        {
            FileName = Path.GetFileNameWithoutExtension(path),
            FilePath = path
        };

        await ExtractUnitypackage(unitypackage);
    }

    public async Task<bool> ExtractUnitypackage(
        SearchEverythingModel unitypackage,
        IProgress<(int extracted, int total)> fileProgress = null)
    {
        try
        {
            var tempFolder = await GetTempFolderPath(unitypackage);
            var targetFolder = await GetTargetFolderPath(unitypackage);

            await CreateDirectories(tempFolder, targetFolder);

            var progress = new Progress<(int extracted, int total)>(value =>
            {
                ConfigHandler.Instance.Config.CurrentExtractedCount = value.extracted;
                ConfigHandler.Instance.Config.TotalFilesToExtract = value.total;
            });

            await ExtractAndWriteFiles(unitypackage, tempFolder, progress);
            await MoveFilesFromTempToTargetFolder(tempFolder, targetFolder);
            Directory.Delete(tempFolder, true);

            var extractionHelper = new ExtractionHelper();

            // Directly update properties to ensure no double counting
            var extractedPackage = new ExtractedUnitypackageModel
            {
                UnitypackageName = unitypackage.FileName ?? "Unknown name",
                UnitypackagePath = targetFolder,
                UnitypackageExtractedDate = DateTime.Now,
                UnitypackageSize = new FileSizeConverter().Convert(
                    await ExtractionHelper.GetTotalSizeInBytesAsync(targetFolder),
                    typeof(string), null, CultureInfo.CurrentCulture)?.ToString(),

                UnitypackageTotalFolderCount = await ExtractionHelper.GetTotalFolderCount(targetFolder),
                UnitypackageTotalFileCount = await ExtractionHelper.GetTotalFileCount(targetFolder),
                UnitypackageTotalScriptCount = await ExtractionHelper.GetTotalScriptCount(targetFolder),
                UnitypackageTotalMaterialCount = await ExtractionHelper.GetTotalMaterialCount(targetFolder),
                UnitypackageTotal3DObjectCount = await ExtractionHelper.GetTotal3DObjectCount(targetFolder),
                UnitypackageTotalImageCount = await ExtractionHelper.GetTotalImageCount(targetFolder),
                UnitypackageTotalAudioCount = await ExtractionHelper.GetTotalAudioCount(targetFolder),
                UnitypackageTotalControllerCount = await ExtractionHelper.GetTotalControllerCount(targetFolder),
                UnitypackageTotalConfigurationCount = await ExtractionHelper.GetTotalConfigurationCount(targetFolder),
                UnitypackageTotalAnimationCount = await ExtractionHelper.GetTotalAnimationCount(targetFolder),
                UnitypackageTotalAssetCount = await ExtractionHelper.GetTotalAssetCount(targetFolder),
                UnitypackageTotalSceneCount = await ExtractionHelper.GetTotalSceneCount(targetFolder),
                UnitypackageTotalShaderCount = await ExtractionHelper.GetTotalShaderCount(targetFolder),
                UnitypackageTotalPrefabCount = await ExtractionHelper.GetTotalPrefabCount(targetFolder),
                UnitypackageTotalFontCount = await ExtractionHelper.GetTotalFontCount(targetFolder),
                UnitypackageTotalDataCount = await ExtractionHelper.GetTotalDataCount(targetFolder),

                HasEncryptedDll = await CheckForEncryptedDlls(targetFolder, extractionHelper),
                MalicousDiscordWebhookCount = await ExtractionHelper.GetMalicousDiscordWebhookCount(targetFolder),
                LinkDetectionCount = await ExtractionHelper.GetTotalLinkDetectionCount(targetFolder)
            };

            // Batch UI update
            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                ConfigHandler.Instance.Config.ExtractedUnitypackages.Add(extractedPackage);
                ConfigHandler.Instance.Config.TotalExtracted =
                    ConfigHandler.Instance.Config.ExtractedUnitypackages.Count;

                // Update TotalFilesExtracted just once clearly after extraction
                ConfigHandler.Instance.Config.TotalFilesExtracted =
                    ConfigHandler.Instance.Config.ExtractedUnitypackages.Sum(pkg => pkg.UnitypackageTotalFileCount);
            });

            await BetterLogger.LogAsync($"Successfully extracted {unitypackage.FileName}", Importance.Info);
            return true;
        }
        catch (Exception e)
        {
            await BetterLogger.LogAsync($"Error while extracting unitypackage: {e.Message}", Importance.Error);
            return false;
        }
    }

    private static async Task<bool> CheckForEncryptedDlls(string folder, ExtractionHelper helper)
    {
        var dllFiles = Directory.GetFiles(folder, "*.dll", SearchOption.AllDirectories);
        foreach (var dll in dllFiles)
            if (await helper.IsEncryptedDll(dll))
                return true;

        return false;
    }


    private static async Task<string> GetTempFolderPath(SearchEverythingModel unitypackage)
    {
        if (unitypackage.FileName != null)
        {
            var tempFolder = Path.Combine(ConfigHandler.Instance.Config.DefaultTempPath, unitypackage.FileName);
            await DeleteIfDirectoryExists(tempFolder);
            await BetterLogger.LogAsync($"Temporary folder path set to: {tempFolder}",
                Importance.Info);
            return tempFolder;
        }

        return string.Empty;
    }

    private static async Task<string> GetTargetFolderPath(SearchEverythingModel unitypackage)
    {
        if (unitypackage.FileName != null)
        {
            var targetFolder = Path.Combine(ConfigHandler.Instance.Config.LastExtractedPath,
                unitypackage.FileName);
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
            if (dir.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            {
                await BetterLogger.LogAsync($"Invalid directory path: {dir}", Importance.Error);
                throw new InvalidOperationException($"Invalid directory path: {dir}");
            }

            Directory.CreateDirectory(dir);
            await BetterLogger.LogAsync($"Created directory: {dir}", Importance.Info);
        }
    }

    private static async Task ExtractAndWriteFiles(
        SearchEverythingModel unitypackage,
        string tempFolder,
        IProgress<(int extracted, int total)>? fileProgress = null)
    {
        var extractedCount = 0;

        // Cache entries only once
        var entries = new List<IEntry>();
        await using (var inStream = File.OpenRead(unitypackage.FilePath!))
        await using (var gzipStream = new GZipStream(inStream, CompressionMode.Decompress))
        using (var reader = TarReader.Open(gzipStream))
        {
            while (reader.MoveToNextEntry())
            {
                var entry = reader.Entry;
                if (!entry.IsEncrypted && !entry.IsDirectory)
                    entries.Add(entry);
            }
        }

        var totalValidEntries = entries.Count;

        // Initialize UI progress only once
        Application.Current.Dispatcher.InvokeAsync(() =>
        {
            ConfigHandler.Instance.Config.TotalFilesToExtract = totalValidEntries;
            ConfigHandler.Instance.Config.CurrentExtractedCount = 0;
        });

        // Perform extraction asynchronously
        await Task.Run(async () =>
        {
            await using var inStream = File.OpenRead(unitypackage.FilePath!);
            await using var gzipStream = new GZipStream(inStream, CompressionMode.Decompress);
            using var reader = TarReader.Open(gzipStream);

            while (reader.MoveToNextEntry())
            {
                var entry = reader.Entry;
                if (entry.IsEncrypted || entry.IsDirectory)
                    continue;

                var filePath = Path.Combine(tempFolder, entry.Key!);
                Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? string.Empty);

                try
                {
                    await using (var fileStream = File.Create(filePath))
                    {
                        reader.WriteEntryTo(fileStream);
                    }

                    extractedCount++;

                    // Update progress only after each file extraction
                    fileProgress?.Report((extractedCount, totalValidEntries));

                    // Batch UI updates to reduce lag
                    if (extractedCount % 5 == 0 || extractedCount == totalValidEntries)
                    {
                        var count = extractedCount;
                        Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            ConfigHandler.Instance.Config.CurrentExtractedCount = count;
                            ConfigHandler.Instance.Config.TotalFilesExtracted++;
                        });
                    }
                    else
                        ConfigHandler.Instance.Config.TotalFilesExtracted++;
                }
                catch (Exception ex)
                {
                    await BetterLogger.LogAsync($"Failed to extract {entry.Key}: {ex.Message}", Importance.Error);
                }
            }
        });
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
                    await MoveFileIfExists(d, "preview.png", targetFullPath,
                        targetFullFile + ".EASYEXTRACTPREVIEW.png");
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

    private static async Task MoveFileIfExists(string directory, string fileName, string targetFullPath,
        string targetFullFile)
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
            await BetterLogger.LogAsync(
                $"I/O error while moving file {sourceFilePath} to {targetFullFile}: {ioEx.Message}",
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