using System.Formats.Tar;
using System.IO.Compression;

namespace EasyExtractCrossPlatform.Services;

public sealed partial class UpdateService
{
    private static async Task DownloadAssetAsync(ReleaseAssetInfo asset, string destinationPath,
        IProgress<UpdateProgress>? progress, CancellationToken cancellationToken)
    {
        using var downloadAssetScope =
            LoggingService.BeginPerformanceScope("DownloadAsset", "Updater", asset.Name);
        var stopwatch = Stopwatch.StartNew();
        LoggingService.LogInformation(
            $"DownloadAsset: start | asset='{asset.Name}' | size={asset.Size} | destination='{destinationPath}'");

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, asset.DownloadUri);
            request.Headers.UserAgent.ParseAdd(UserAgent);
            request.Headers.Accept.ParseAdd("application/octet-stream");

            using var response = await HttpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            await using var responseStream =
                await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

            FileStream? fileStream = null;
            const int maxRetries = 3;
            for (var i = 0; i < maxRetries; i++)
                try
                {
                    fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
                    break;
                }
                catch (IOException ex)
                {
                    if (i == maxRetries - 1)
                    {
                        LoggingService.LogError(
                            $"DownloadAsset: failed to create file stream after {maxRetries} attempts | path='{destinationPath}'",
                            ex);
                        throw;
                    }

                    LoggingService.LogWarning(
                        $"DownloadAsset: file locked, retrying... ({i + 1}/{maxRetries}) | path='{destinationPath}'");
                    await Task.Delay(500 * (i + 1), cancellationToken).ConfigureAwait(false);
                }

            await using var fs = fileStream!;

            var buffer = new byte[81920];
            long totalRead = 0;
            while (true)
            {
                var read = await responseStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)
                    .ConfigureAwait(false);
                if (read == 0)
                    break;

                await fs.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                totalRead += read;

                double? percentage = null;
                if (asset.Size > 0)
                    percentage = Math.Clamp((double)totalRead / asset.Size, 0, 1);

                progress?.Report(new UpdateProgress(UpdatePhase.Downloading, percentage, totalRead, asset.Size));
            }

            stopwatch.Stop();
            LoggingService.LogInformation(
                $"DownloadAsset: completed | asset='{asset.Name}' | bytesReceived={totalRead} | destination='{destinationPath}'");
            LoggingService.LogPerformance("DownloadAsset", stopwatch.Elapsed, "Updater",
                $"asset={asset.Name} | destination={destinationPath}", totalRead);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LoggingService.LogError(
                $"DownloadAsset: failure | asset='{asset.Name}' | destination='{destinationPath}'", ex);
            throw;
        }
    }

    private static async Task ExtractArchiveAsync(string archivePath, string destinationDirectory,
        CancellationToken cancellationToken)
    {
        var fileName = Path.GetFileName(archivePath);
        using var extractArchiveScope =
            LoggingService.BeginPerformanceScope("ExtractArchive", "Updater", fileName);
        var stopwatch = Stopwatch.StartNew();
        LoggingService.LogInformation(
            $"ExtractArchive: start | archive='{fileName}' | destination='{destinationDirectory}'");

        try
        {
            if (fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                ZipFile.ExtractToDirectory(archivePath, destinationDirectory, true);
                stopwatch.Stop();
                LoggingService.LogInformation(
                    $"ExtractArchive: zip completed | destination='{destinationDirectory}'");
                LoggingService.LogPerformance("ExtractArchive", stopwatch.Elapsed, "Updater",
                    $"archive={fileName} | type=zip");
                return;
            }

            if (fileName.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) ||
                fileName.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase))
            {
                await using var fileStream = File.OpenRead(archivePath);
                await using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
                TarFile.ExtractToDirectory(gzipStream, destinationDirectory, true);
                stopwatch.Stop();
                LoggingService.LogInformation(
                    $"ExtractArchive: tar.gz completed | destination='{destinationDirectory}'");
                LoggingService.LogPerformance("ExtractArchive", stopwatch.Elapsed, "Updater",
                    $"archive={fileName} | type=tar.gz");
                return;
            }

            LoggingService.LogError(
                $"ExtractArchive: unsupported format | archive='{fileName}' | destination='{destinationDirectory}'");
            throw new NotSupportedException($"Unsupported archive format for '{fileName}'.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LoggingService.LogError(
                $"ExtractArchive: failure | archive='{archivePath}' | destination='{destinationDirectory}'", ex);
            throw;
        }
    }

    private static string ResolveContentRoot(string payloadDirectory)
    {
        var directories = Directory
            .GetDirectories(payloadDirectory)
            .Where(dir =>
            {
                var name = Path.GetFileName(dir);
                return !string.Equals(name, "__MACOSX", StringComparison.OrdinalIgnoreCase);
            })
            .ToList();

        var files = Directory.GetFiles(payloadDirectory)
            .Where(file =>
            {
                var name = Path.GetFileName(file);
                return !name.StartsWith(".", StringComparison.Ordinal);
            })
            .ToList();

        var resolved = directories.Count == 1 && files.Count == 0
            ? directories[0]
            : payloadDirectory;

        LoggingService.LogInformation(
            $"ResolveContentRoot: resolved | payload='{payloadDirectory}' | directories={directories.Count} | files={files.Count} | resolved='{resolved}'");

        return resolved;
    }
}