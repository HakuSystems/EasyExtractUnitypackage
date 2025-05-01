using System.Globalization;
using System.IO.Compression;
using EasyExtract.Config;
using EasyExtract.Config.Models;
using EasyExtract.Utilities;
using Notification.Wpf;
using SharpCompress.Readers.Tar;

namespace EasyExtract.Services;

public class ExtractionHandler
{
    private static readonly NotificationService NotificationService = new();

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
        IProgress<(int extracted, int total)>? fileProgress = null)
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

            // Directly update properties to ensure no double counting
            var extractedPackage = new ExtractedUnitypackageModel
            {
                UnitypackageName = unitypackage.FileName ?? "Unknown name",
                UnitypackagePath = targetFolder,
                UnitypackageExtractedDate = DateTime.Now,
                UnitypackageSize = new FileSizeConverter().Convert(
                        await ExtractionHelper.GetTotalSizeInBytesAsync(targetFolder),
                        typeof(string), null, CultureInfo.CurrentCulture)
                    .ToString(),

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

                HasEncryptedDll = await CheckForEncryptedDlls(targetFolder),
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
                NotificationService.ShowNotification(
                    "Successfully Extracted!",
                    $"Unitypackage {unitypackage.FileName} has been successfully extracted to {targetFolder}",
                    NotificationType.Success);
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


    private static async Task<bool> CheckForEncryptedDlls(string folder)
    {
        var dllFiles = Directory.GetFiles(folder, "*.dll", SearchOption.AllDirectories);
        foreach (var dll in dllFiles)
            if (await ExtractionHelper.IsEncryptedDll(dll))
                return true;

        return false;
    }


    private static async Task<string> GetTempFolderPath(SearchEverythingModel unitypackage)
    {
        if (string.IsNullOrEmpty(unitypackage.FileName))
        {
            await BetterLogger.LogAsync("Unitypackage filename is null or empty", Importance.Error);
            return Path.Combine(ConfigHandler.Instance.Config.DefaultTempPath, "UnknownPackage_" + Guid.NewGuid());
        }

        // Sanitize filename to avoid path issues
        var sanitizedName = string.Join("_", unitypackage.FileName.Split(Path.GetInvalidFileNameChars()));
        var tempFolder = Path.Combine(ConfigHandler.Instance.Config.DefaultTempPath, sanitizedName);
        await DeleteIfDirectoryExists(tempFolder);
        await BetterLogger.LogAsync($"Temporary folder path set to: {tempFolder}", Importance.Info);
        return tempFolder;
    }

    private static async Task<string> GetTargetFolderPath(SearchEverythingModel unitypackage)
    {
        if (string.IsNullOrEmpty(unitypackage.FileName))
        {
            await BetterLogger.LogAsync("Unitypackage filename is null or empty", Importance.Error);
            return Path.Combine(ConfigHandler.Instance.Config.DefaultOutputPath, "UnknownPackage_" + Guid.NewGuid());
        }

        // Sanitize filename to avoid path issues
        var sanitizedName = string.Join("_", unitypackage.FileName.Split(Path.GetInvalidFileNameChars()));
        var targetFolder = Path.Combine(ConfigHandler.Instance.Config.DefaultOutputPath, sanitizedName);
        await DeleteIfDirectoryExists(targetFolder);
        await BetterLogger.LogAsync($"Target folder path set to: {targetFolder}", Importance.Info);
        return targetFolder;
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
        if (string.IsNullOrEmpty(unitypackage.FilePath))
        {
            await BetterLogger.LogAsync("Unitypackage file path is null or empty", Importance.Error);
            return;
        }

        var extractedCount = 0;
        var totalValidEntries = 0;

        // First pass: count valid entries without storing them in memory
        try
        {
            await using (var inStream = File.OpenRead(unitypackage.FilePath))
            await using (var gzipStream = new GZipStream(inStream, CompressionMode.Decompress))
            using (var reader = TarReader.Open(gzipStream))
            {
                while (reader.MoveToNextEntry())
                    if (!reader.Entry.IsEncrypted && !reader.Entry.IsDirectory)
                        totalValidEntries++;
            }
        }
        catch (Exception ex)
        {
            await BetterLogger.LogAsync($"Error counting entries in unitypackage: {ex.Message}", Importance.Error);
            return;
        }

        // Initialize UI progress only once
        Application.Current.Dispatcher.InvokeAsync(() =>
        {
            ConfigHandler.Instance.Config.TotalFilesToExtract = totalValidEntries;
            ConfigHandler.Instance.Config.CurrentExtractedCount = 0;
        });

        if (totalValidEntries == 0)
        {
            await BetterLogger.LogAsync("No valid entries found in unitypackage", Importance.Warning);
            return;
        }

        // Second pass: extract files
        try
        {
            // Perform extraction asynchronously
            await Task.Run(async () =>
            {
                await using var inStream = File.OpenRead(unitypackage.FilePath);
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
                        {
                            ConfigHandler.Instance.Config.TotalFilesExtracted++;
                        }
                    }
                    catch (Exception ex)
                    {
                        await BetterLogger.LogAsync($"Failed to extract {entry.Key}: {ex.Message}", Importance.Error);
                    }
                }
            });
        }
        catch (Exception ex)
        {
            await BetterLogger.LogAsync($"Error during extraction: {ex.Message}", Importance.Error);
        }
    }

    private static async Task MoveFilesFromTempToTargetFolder(string tempFolder, string targetFolder)
    {
        foreach (var directory in Directory.EnumerateDirectories(tempFolder))
        {
            string? targetFullPath;
            string? targetFullFile = null;

            try
            {
                var pathnameFile = Path.Combine(directory, "pathname");

                if (File.Exists(pathnameFile))
                {
                    var hashPathName = (await File.ReadAllTextAsync(pathnameFile)).Trim();

                    if (string.IsNullOrWhiteSpace(hashPathName))
                    {
                        await BetterLogger.LogAsync($"Empty pathname file in {directory}. Skipping file move.",
                            Importance.Warning);
                        continue;
                    }

                    // Clean Unicode and control characters
                    hashPathName = new string(hashPathName.Where(c =>
                        char.GetUnicodeCategory(c) != UnicodeCategory.Format &&
                        char.GetUnicodeCategory(c) != UnicodeCategory.Control).ToArray());

                    // Remove trailing "00"
                    if (hashPathName.EndsWith("00"))
                        hashPathName = hashPathName[..^2];

                    // Normalize file extension
                    hashPathName = NormalizeFileExtension(hashPathName);

                    // Replace any invalid path characters
                    var sanitizedPath = string.Join("_", hashPathName.Split(Path.GetInvalidPathChars()));

                    // Handle potential path traversal attempts
                    if (sanitizedPath.Contains("..") || sanitizedPath.Contains("./") || sanitizedPath.Contains("/."))
                    {
                        sanitizedPath = sanitizedPath.Replace("..", "_").Replace("./", "_").Replace("/.", "_");
                        await BetterLogger.LogAsync($"Suspicious path detected and sanitized: {hashPathName}",
                            Importance.Warning);
                    }

                    try
                    {
                        targetFullPath = Path.GetDirectoryName(Path.Combine(targetFolder, sanitizedPath));
                        targetFullFile = Path.Combine(targetFolder, sanitizedPath);
                    }
                    catch (Exception pathEx)
                    {
                        await BetterLogger.LogAsync($"Error creating path: {pathEx.Message}. Using fallback path.",
                            Importance.Error);
                        // Fallback to a safe path if there's still an issue
                        var fallbackName = $"UnknownAsset_{Guid.NewGuid()}";
                        targetFullPath = Path.Combine(targetFolder, "FallbackAssets");
                        targetFullFile = Path.Combine(targetFullPath, fallbackName);
                    }

                    if (string.IsNullOrWhiteSpace(targetFullPath) || string.IsNullOrWhiteSpace(targetFullFile))
                    {
                        await BetterLogger.LogAsync(
                            $"Derived empty target path/file for {directory}. Skipping file move.", Importance.Warning);
                        continue;
                    }
                }
                else
                {
                    await BetterLogger.LogAsync($"Missing 'pathname' file in {directory}. Skipping file move.",
                        Importance.Warning);
                    continue;
                }

                // Proceed safely after validation
                await MoveFileIfExists(directory, "asset", targetFullPath, targetFullFile);
                await MoveFileIfExists(directory, "asset.meta", targetFullPath, targetFullFile + ".meta");
                await MoveFileIfExists(directory, "preview.png", targetFullPath,
                    targetFullFile + ".EASYEXTRACTPREVIEW.png");
            }
            catch (Exception ex)
            {
                await BetterLogger.LogAsync(
                    $"Error moving file from {directory} to {targetFullFile ?? "[unknown path]"}: {ex.Message}",
                    Importance.Error);
            }
        }

        await BetterLogger.LogAsync($"Moved files from temporary folder to target folder: {targetFolder}",
            Importance.Info);
    }


    // Credits oguzhan_sparklegames
    private static string NormalizeFileExtension(string filename)
    {
        var lastDotIndex = filename.LastIndexOf('.');
        if (lastDotIndex < 0)
            return filename;

        var nameWithoutExtension = filename[..lastDotIndex];
        var extensionCandidate = filename[lastDotIndex..];

        var matchedExtension = ExtractionHelper.ValidExtensions
            .OrderByDescending(e => e.Length)
            .FirstOrDefault(validExt => extensionCandidate.StartsWith(validExt, StringComparison.OrdinalIgnoreCase));

        // If a valid extension clearly matches, use it, else leave filename unchanged
        return matchedExtension != null ? nameWithoutExtension + matchedExtension : filename;
    }


    private static async Task MoveFileIfExists(string directory, string fileName, string? targetFullPath,
        string? targetFullFile)
    {
        var sourceFilePath = Path.Combine(directory, fileName);
        if (!File.Exists(sourceFilePath)) return;
        if (targetFullPath == null || targetFullFile == null) return;

        const int maxRetries = 3;
        const int delayMs = 100;

        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                // Create directory if it doesn't exist
                Directory.CreateDirectory(targetFullPath);

                // Try to copy instead of move if this is a retry attempt
                if (attempt > 1)
                {
                    await BetterLogger.LogAsync(
                        $"Attempt {attempt}: Trying to copy instead of move {sourceFilePath} to {targetFullFile}",
                        Importance.Warning);

                    File.Copy(sourceFilePath, targetFullFile, true);

                    // Try to delete the source file, but don't fail if we can't
                    try
                    {
                        File.Delete(sourceFilePath);
                    }
                    catch
                    {
                        // Ignore deletion errors - at least we copied the file
                    }
                }
                else
                {
                    // First attempt - try to move
                    File.Move(sourceFilePath, targetFullFile, true);
                }

                // If we get here, the operation succeeded
                return;
            }
            catch (IOException ioEx)
            {
                if (attempt == maxRetries)
                    await BetterLogger.LogAsync(
                        $"I/O error while moving file {sourceFilePath} to {targetFullFile} after {maxRetries} attempts: {ioEx.Message}",
                        Importance.Error);
                else
                    // Wait before retrying
                    await Task.Delay(delayMs * attempt);
            }
            catch (UnauthorizedAccessException uaEx)
            {
                await BetterLogger.LogAsync(
                    $"Access denied while moving file {sourceFilePath} to {targetFullFile}: {uaEx.Message}",
                    Importance.Error);
                break; // Don't retry access issues
            }
            catch (Exception ex)
            {
                await BetterLogger.LogAsync(
                    $"Unexpected error while moving file {sourceFilePath} to {targetFullFile}: {ex.Message}",
                    Importance.Error);
                break; // Don't retry unknown issues
            }
        }
    }
}