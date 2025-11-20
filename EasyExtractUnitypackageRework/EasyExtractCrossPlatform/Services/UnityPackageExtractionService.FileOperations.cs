using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EasyExtractCrossPlatform.Models;
using EasyExtractCrossPlatform.Utilities;

namespace EasyExtractCrossPlatform.Services;

public sealed partial class UnityPackageExtractionService
{
    private static void TrackCorruptedDirectories(
        string outputDirectory,
        UnityPackageAssetState state,
        HashSet<string> directoriesToCleanup)
    {
        if (directoriesToCleanup is null)
            return;

        var normalizations = state.PathNormalizations;
        if (normalizations is null || normalizations.Count <= 1)
            return;

        string? originalAccumulated = null;
        for (var i = 0; i < normalizations.Count - 1; i++)
        {
            var normalization = normalizations[i];
            var originalSegment = normalization.Original;

            if (string.IsNullOrWhiteSpace(originalSegment))
                continue;

            originalAccumulated = originalAccumulated is null
                ? originalSegment
                : Path.Combine(originalAccumulated, originalSegment);

            if (string.Equals(
                    normalization.Original,
                    normalization.Normalized,
                    StringComparison.Ordinal))
                continue;

            if (string.IsNullOrWhiteSpace(originalAccumulated))
                continue;

            var candidate = Path.Combine(outputDirectory, originalAccumulated);
            directoriesToCleanup.Add(candidate);
        }
    }


    private static void CleanupCorruptedDirectories(HashSet<string> directoriesToCleanup, string correlationId)
    {
        if (directoriesToCleanup.Count == 0)
            return;

        LoggingService.LogInformation(
            $"CleanupCorruptedDirectories started | count={directoriesToCleanup.Count} | correlationId={correlationId}");
        var deletedCount = 0;

        foreach (var directory in directoriesToCleanup
                     .OrderByDescending(path => path.Length))
            try
            {
                if (!Directory.Exists(directory))
                    continue;

                if (Directory.EnumerateFileSystemEntries(directory).Any())
                    continue;

                Directory.Delete(directory);
                deletedCount++;
            }
            catch (IOException ex)
            {
                LoggingService.LogWarning(
                    $"Failed to delete empty directory | path='{directory}' | correlationId={correlationId}", ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                LoggingService.LogWarning(
                    $"Access denied when deleting directory | path='{directory}' | correlationId={correlationId}", ex);
            }

        if (deletedCount > 0)
            LoggingService.LogInformation(
                $"CleanupCorruptedDirectories completed | deleted={deletedCount}/{directoriesToCleanup.Count} | correlationId={correlationId}");
    }


    private static AssetWritePlan CreateAssetWritePlan(
        UnityPackageAssetState state,
        string targetPath,
        string? metaPath,
        string? previewPath)
    {
        var writeAsset = state.Asset is { HasContent: true } && NeedsWrite(targetPath, state.Asset);
        var writeMeta = metaPath is not null &&
                        state.Meta is { HasContent: true } &&
                        NeedsWrite(metaPath, state.Meta);
        var writePreview = previewPath is not null &&
                           state.Preview is { HasContent: true } &&
                           NeedsWrite(previewPath, state.Preview);

        return new AssetWritePlan(writeAsset, writeMeta, writePreview);
    }


    private static IReadOnlyList<string> WriteAssetToDisk(
        UnityPackageAssetState state,
        string targetPath,
        string? metaPath,
        string? previewPath,
        AssetWritePlan plan,
        CancellationToken cancellationToken,
        string correlationId)
    {
        if (!plan.RequiresWrite)
            return Array.Empty<string>();

        var writtenFiles = new List<string>();
        var directory = Path.GetDirectoryName(targetPath);
        var directoryEnsured = false;

        void EnsureDirectory()
        {
            if (directoryEnsured)
                return;

            if (!string.IsNullOrWhiteSpace(directory))
            {
                try
                {
                    Directory.CreateDirectory(directory);
                }
                catch (Exception ex)
                {
                    LoggingService.LogError(
                        $"Failed to create asset directory | path='{directory}' | correlationId={correlationId}", ex);
                    throw;
                }
            }

            directoryEnsured = true;
        }

        if (plan.WriteAsset && state.Asset is { HasContent: true } assetComponent)
        {
            EnsureDirectory();
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                assetComponent.CopyTo(targetPath, cancellationToken);
                writtenFiles.Add(targetPath);
            }
            catch (Exception ex)
            {
                LoggingService.LogError(
                    $"Failed to write asset | path='{targetPath}' | size={assetComponent.Length} | correlationId={correlationId}",
                    ex);
                throw;
            }
        }

        if (plan.WriteMeta && metaPath is not null && state.Meta is { HasContent: true } metaComponent)
        {
            EnsureDirectory();
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                metaComponent.CopyTo(metaPath, cancellationToken);
                writtenFiles.Add(metaPath);
            }
            catch (Exception ex)
            {
                LoggingService.LogError(
                    $"Failed to write meta file | path='{metaPath}' | correlationId={correlationId}", ex);
                throw;
            }
        }

        if (plan.WritePreview && previewPath is not null && state.Preview is { HasContent: true } previewComponent)
        {
            EnsureDirectory();
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                previewComponent.CopyTo(previewPath, cancellationToken);
                writtenFiles.Add(previewPath);
            }
            catch (Exception ex)
            {
                LoggingService.LogError(
                    $"Failed to write preview | path='{previewPath}' | correlationId={correlationId}", ex);
                throw;
            }
        }

        return writtenFiles;
    }


    private static bool NeedsWrite(string path, AssetComponent component)
    {
        if (!File.Exists(path))
            return true;

        return !ContentMatches(path, component);
    }


    private static bool ContentMatches(string path, AssetComponent component)
    {
        try
        {
            var fileInfo = new FileInfo(path);
            if (!fileInfo.Exists || fileInfo.Length != component.Length)
                return false;

            using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var fileHash = SHA256.HashData(stream);
            return CryptographicOperations.FixedTimeEquals(fileHash, component.ContentHash.Span);
        }
        catch (IOException ex)
        {
            LoggingService.LogWarning($"IOException during content comparison | path='{path}'", ex);
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            LoggingService.LogWarning($"Access denied during content comparison | path='{path}'", ex);
            return false;
        }
        catch (CryptographicException ex)
        {
            LoggingService.LogWarning($"Cryptographic error during content comparison | path='{path}'", ex);
            return false;
        }
    }


    private static string ReadEntryAsUtf8String(Stream dataStream, CancellationToken cancellationToken,
        string correlationId)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var reader = new StreamReader(
            dataStream,
            Encoding.UTF8,
            true,
            1024,
            true);
        var buffer = new char[512];
        var builder = new StringBuilder();
        var totalRead = 0;

        int read;
        while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
        {
            totalRead += read;
            if (totalRead > MaxPathEntryCharacters)
            {
                LoggingService.LogError(
                    $"Path entry exceeded max length | length={totalRead} | max={MaxPathEntryCharacters} | correlationId={correlationId}");
                throw new InvalidDataException(
                    $"Path entry exceeded the maximum supported length of {MaxPathEntryCharacters:N0} characters.");
            }

            builder.Append(buffer, 0, read);
            cancellationToken.ThrowIfCancellationRequested();
        }

        return builder.ToString();
    }


    private static AssetComponent? CreateAssetComponent(
        TarEntry entry,
        string temporaryDirectory,
        UnityPackageExtractionLimits limits,
        ExtractionLimiter limiter,
        CancellationToken cancellationToken,
        string correlationId)
    {
        if (entry.DataStream is null)
            return null;

        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(temporaryDirectory);
        var entryName = string.IsNullOrWhiteSpace(entry.Name) ? "asset" : entry.Name;
        limiter.ValidateDeclaredSize(entry.Length, entryName);

        var tempPath = Path.Combine(temporaryDirectory, $"{Guid.NewGuid():N}.tmp");
        FileStream? output = null;

        try
        {
            using (LoggingService.BeginPerformanceScope("CreateAssetComponent", "Extraction",
                       correlationId))
            {
                output = File.Open(tempPath, FileMode.Create, FileAccess.Write, FileShare.None);
                using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                var buffer = ArrayPool<byte>.Shared.Rent(StreamCopyBufferSize);
                long totalWritten = 0;

                try
                {
                    int read;
                    while ((read = entry.DataStream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        output.Write(buffer, 0, read);
                        hasher.AppendData(buffer, 0, read);
                        totalWritten += read;

                        if (limits.MaxAssetBytes > 0 && totalWritten > limits.MaxAssetBytes)
                        {
                            LoggingService.LogError(
                                $"Asset exceeded per-file limit | entry='{entryName}' | size={totalWritten} | limit={limits.MaxAssetBytes} | correlationId={correlationId}");
                            throw new InvalidDataException(
                                $"Asset '{entryName}' exceeded the configured per-file limit of {limits.MaxAssetBytes:N0} bytes.");
                        }
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }

                output.Flush();
                var hash = hasher.GetHashAndReset();
                output.Dispose();

                if (totalWritten == 0)
                {
                    TryDeleteFile(tempPath);
                    LoggingService.LogInformation(
                        $"Asset component empty, skipping | entry='{entryName}' | correlationId={correlationId}");
                    return null;
                }

                limiter.TrackAssetBytes(totalWritten);
                return new AssetComponent(tempPath, totalWritten, hash);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not InvalidDataException)
        {
            output?.Dispose();
            TryDeleteFile(tempPath);
            LoggingService.LogError(
                $"Failed to create asset component | entry='{entryName}' | correlationId={correlationId}", ex);
            throw;
        }
        catch
        {
            output?.Dispose();
            TryDeleteFile(tempPath);
            throw;
        }
    }


    private static TemporaryDirectoryScope CreateTemporaryDirectory(string? baseDirectory, string correlationId)
    {
        var root = string.IsNullOrWhiteSpace(baseDirectory)
            ? Path.Combine(Path.GetTempPath(), "EasyExtractCrossPlatform")
            : baseDirectory!;

        try
        {
            Directory.CreateDirectory(root);
        }
        catch (Exception ex)
        {
            LoggingService.LogError(
                $"Failed to create temp root directory | path='{root}' | correlationId={correlationId}", ex);
            throw;
        }

        var scopedDirectory = Path.Combine(root, Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(scopedDirectory);
            LoggingService.LogInformation(
                $"Created temporary directory | path='{scopedDirectory}' | correlationId={correlationId}");
            return new TemporaryDirectoryScope(scopedDirectory, correlationId);
        }
        catch (Exception ex)
        {
            LoggingService.LogError(
                $"Failed to create scoped temp directory | path='{scopedDirectory}' | correlationId={correlationId}",
                ex);
            throw;
        }
    }


    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException ex)
        {
            LoggingService.LogWarning($"Failed to delete temporary file | path='{path}'", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            LoggingService.LogWarning($"Access denied when deleting temporary file | path='{path}'", ex);
        }
    }


    private static (string AssetKey, string ComponentName) SplitEntryName(string entryName)
    {
        var normalized = entryName.Replace('\\', '/');
        var firstSlash = normalized.IndexOf('/');
        if (firstSlash < 0)
            return (string.Empty, string.Empty);

        var key = normalized[..firstSlash].Trim();
        var remainder = normalized[(firstSlash + 1)..].Trim();
        return (key, remainder.ToLowerInvariant());
    }

}
