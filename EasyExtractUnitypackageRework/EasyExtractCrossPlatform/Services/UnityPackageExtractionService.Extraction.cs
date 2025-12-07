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

        // Check for GZip magic bytes (0x1F, 0x8B)
        var buffer = new byte[2];
        var bytesRead = packageStream.Read(buffer, 0, 2);
        packageStream.Position = 0; // Reset position

        Stream inputStream = packageStream;
        GZipInputStream? gzipStream = null;

        if (bytesRead == 2 && buffer[0] == 0x1F && buffer[1] == 0x8B)
        {
            gzipStream = new GZipInputStream(packageStream);
            inputStream = gzipStream;
        }

        using var tarReader = new TarInputStream(inputStream, Encoding.UTF8);

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

        var result = session.Execute();
        gzipStream?.Dispose();
        return result;
    }
}