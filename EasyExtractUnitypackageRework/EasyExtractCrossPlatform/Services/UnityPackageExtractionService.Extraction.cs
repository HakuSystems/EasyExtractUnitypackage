using System.Formats.Tar;
using System.IO.Compression;

namespace EasyExtractCrossPlatform.Services;

public sealed partial class UnityPackageExtractionService
{
    private static UnityPackageExtractionResult ExtractInternal(
        string packagePath,
        string outputDirectory,
        UnityPackageExtractionOptions options,
        IProgress<UnityPackageExtractionProgress>? progress,
        CancellationToken cancellationToken,
        string correlationId)
    {
        LoggingService.LogInformation(
            $"ExtractInternal started | package='{packagePath}' | correlationId={correlationId}");

        using var packageStream = File.OpenRead(packagePath);
        using var gzipStream = new GZipStream(packageStream, CompressionMode.Decompress);
        using var tarReader = new TarReader(gzipStream);

        var normalizedOutputDirectory = NormalizeOutputDirectory(outputDirectory);
        var limits = UnityPackageExtractionLimits.Normalize(options.Limits);
        using var temporaryDirectory = CreateTemporaryDirectory(options.TemporaryDirectory, correlationId);

        var session = new ExtractionSession(
            packagePath,
            outputDirectory,
            normalizedOutputDirectory,
            options.OrganizeByCategories,
            limits,
            temporaryDirectory.DirectoryPath,
            tarReader,
            progress,
            cancellationToken,
            correlationId);

        return session.Execute();
    }
}