using System.Text;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;

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
        using var gzipStream = new GZipInputStream(packageStream);
        using var tarReader = new TarInputStream(gzipStream, Encoding.UTF8);

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