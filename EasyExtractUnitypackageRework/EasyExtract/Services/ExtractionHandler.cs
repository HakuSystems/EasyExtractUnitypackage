using System.Globalization;
using System.IO.Compression;
using EasyExtract.Config;
using EasyExtract.Config.Models;
using EasyExtract.Utilities;
using EasyExtract.Utilities.Logger;
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

            BetterLogger.LogWithContext(
                $"Extraction Summary: {extractedPackage.UnitypackageName} - {extractedPackage.UnitypackagePath}",
                new Dictionary<string, object>
                {
                    ["TotalFilesExtracted"] = extractedPackage.UnitypackageTotalFileCount,
                    ["TotalFoldersExtracted"] = extractedPackage.UnitypackageTotalFolderCount,
                    ["HasEncryptedDll"] = extractedPackage.HasEncryptedDll,
                    ["MalicousDiscordWebhookCount"] = extractedPackage.MalicousDiscordWebhookCount,
                    ["LinkDetectionCount"] = extractedPackage.LinkDetectionCount
                });
            return true;
        }
        catch (Exception e)
        {
            BetterLogger.Exception(e, "Failed to extract unitypackage",
                "Extraction");
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
            BetterLogger.LogWithContext("Unitypackage filename is null or empty",
                new Dictionary<string, object>
                {
                    ["FilePath"] = unitypackage.FilePath ?? "Unknown path"
                }, LogLevel.Warning);
            return Path.Combine(ConfigHandler.Instance.Config.DefaultTempPath, "UnknownPackage_" + Guid.NewGuid());
        }

        // Sanitize filename to avoid path issues
        var sanitizedName = string.Join("_", unitypackage.FileName.Split(Path.GetInvalidFileNameChars()));
        var tempFolder = Path.Combine(ConfigHandler.Instance.Config.DefaultTempPath, sanitizedName);
        await DeleteIfDirectoryExists(tempFolder);
        BetterLogger.LogWithContext("Temporary folder path set",
            new Dictionary<string, object>
            {
                ["TempFolder"] = tempFolder
            });
        return tempFolder;
    }

    private static async Task<string> GetTargetFolderPath(SearchEverythingModel unitypackage)
    {
        if (string.IsNullOrEmpty(unitypackage.FileName))
        {
            BetterLogger.LogWithContext("Unitypackage filename is null or empty",
                new Dictionary<string, object>
                {
                    ["FilePath"] = unitypackage.FilePath ?? "Unknown path"
                }, LogLevel.Warning);
            return Path.Combine(ConfigHandler.Instance.Config.DefaultOutputPath, "UnknownPackage_" + Guid.NewGuid());
        }

        // Sanitize filename to avoid path issues
        var sanitizedName = string.Join("_", unitypackage.FileName.Split(Path.GetInvalidFileNameChars()));
        var targetFolder = Path.Combine(ConfigHandler.Instance.Config.DefaultOutputPath, sanitizedName);
        await DeleteIfDirectoryExists(targetFolder);
        BetterLogger.LogWithContext("Target folder path set",
            new Dictionary<string, object>
            {
                ["TargetFolder"] = targetFolder
            });
        return targetFolder;
    }


    private static async Task DeleteIfDirectoryExists(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
            BetterLogger.LogWithContext("Deleting existing directory",
                new Dictionary<string, object> { ["Directory"] = directory });
        }
    }

    private static async Task CreateDirectories(params string[] directories)
    {
        foreach (var dir in directories)
        {
            if (dir.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            {
                BetterLogger.LogWithContext("Invalid directory path detected",
                    new Dictionary<string, object> { ["Directory"] = dir },
                    LogLevel.Error);

                throw new InvalidOperationException($"Invalid directory path: {dir}");
            }

            Directory.CreateDirectory(dir);
            BetterLogger.LogWithContext("Created directory",
                new Dictionary<string, object> { ["Directory"] = dir });
        }
    }

    private static async Task ExtractAndWriteFiles(
        SearchEverythingModel unitypackage,
        string tempFolder,
        IProgress<(int extracted, int total)>? fileProgress = null)
    {
        if (string.IsNullOrEmpty(unitypackage.FilePath))
        {
            BetterLogger.LogWithContext("Unitypackage file path is null or empty",
                new Dictionary<string, object>(), LogLevel.Error);
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
            BetterLogger.Exception(ex, "Error counting entries in unitypackage",
                "Extraction");
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
            BetterLogger.LogWithContext("No valid entries found in unitypackage",
                new Dictionary<string, object>
                {
                    ["FilePath"] = unitypackage.FilePath
                }, LogLevel.Warning);
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
                        BetterLogger.Exception(ex, "Failed to extract file",
                            "Extraction");
                    }
                }
            });
        }
        catch (Exception ex)
        {
            BetterLogger.Exception(ex, "Error during extraction",
                "Extraction");
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
                        BetterLogger.LogWithContext("Empty pathname file detected, skipping file move",
                            new Dictionary<string, object> { ["Directory"] = directory },
                            LogLevel.Warning);
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
                        BetterLogger.LogWithContext("Suspicious path detected and sanitized",
                            new Dictionary<string, object>
                            {
                                ["OriginalPath"] = hashPathName,
                                ["SanitizedPath"] = sanitizedPath
                            }, LogLevel.Warning);
                    }

                    try
                    {
                        targetFullPath = Path.GetDirectoryName(Path.Combine(targetFolder, sanitizedPath));
                        targetFullFile = Path.Combine(targetFolder, sanitizedPath);
                    }
                    catch (Exception pathEx)
                    {
                        BetterLogger.LogWithContext("Error creating path, using fallback path",
                            new Dictionary<string, object>
                            {
                                ["Error"] = pathEx.Message,
                                ["FallbackName"] = $"UnknownAsset_{Guid.NewGuid()}"
                            }, LogLevel.Error);
                        BetterLogger.Exception(pathEx, "Error creating path",
                            "Extraction");
                        // Fallback to a safe path if there's still an issue
                        var fallbackName = $"UnknownAsset_{Guid.NewGuid()}";
                        targetFullPath = Path.Combine(targetFolder, "FallbackAssets");
                        targetFullFile = Path.Combine(targetFullPath, fallbackName);
                    }

                    if (string.IsNullOrWhiteSpace(targetFullPath) || string.IsNullOrWhiteSpace(targetFullFile))
                    {
                        BetterLogger.LogWithContext("Derived empty target path/file, skipping file move",
                            new Dictionary<string, object>
                            {
                                ["Directory"] = directory,
                                ["TargetPath"] = targetFullPath ?? "[null]",
                                ["TargetFile"] = targetFullFile ?? "[null]"
                            }, LogLevel.Warning);
                        continue;
                    }
                }
                else
                {
                    BetterLogger.LogWithContext("Missing pathname file, skipping file move",
                        new Dictionary<string, object>
                        {
                            ["Directory"] = directory
                        }, LogLevel.Warning);
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
                BetterLogger.LogWithContext("Error moving file",
                    new Dictionary<string, object>
                    {
                        ["FromDirectory"] = directory,
                        ["ToPath"] = targetFullFile ?? "[unknown path]",
                        ["Error"] = ex.Message
                    }, LogLevel.Error);
            }
        }

        BetterLogger.LogWithContext("Moved files from temporary folder",
            new Dictionary<string, object>
            {
                ["TargetFolder"] = targetFolder
            });
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
                    BetterLogger.LogWithContext($"Attempt {attempt}: Trying to copy instead of move",
                        new Dictionary<string, object>
                        {
                            ["SourcePath"] = sourceFilePath,
                            ["TargetPath"] = targetFullFile,
                            ["AttemptNumber"] = attempt
                        }, LogLevel.Warning);

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
                    BetterLogger.LogWithContext($"I/O error while moving file after {maxRetries} attempts",
                        new Dictionary<string, object>
                        {
                            ["SourcePath"] = sourceFilePath,
                            ["TargetPath"] = targetFullFile,
                            ["Error"] = ioEx.Message,
                            ["Attempts"] = maxRetries
                        }, LogLevel.Error);
                else
                    await Task.Delay(delayMs * attempt);
            }
            catch (UnauthorizedAccessException uaEx)
            {
                BetterLogger.LogWithContext("Access denied while moving file",
                    new Dictionary<string, object>
                    {
                        ["SourcePath"] = sourceFilePath,
                        ["TargetPath"] = targetFullFile,
                        ["Error"] = uaEx.Message
                    }, LogLevel.Error);
                break;
            }
            catch (Exception ex)
            {
                BetterLogger.LogWithContext("Unexpected error while moving file",
                    new Dictionary<string, object>
                    {
                        ["SourcePath"] = sourceFilePath,
                        ["TargetPath"] = targetFullFile,
                        ["Error"] = ex.Message
                    }, LogLevel.Error);
                break;
            }
        }
    }
}