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
        var format = UnityPackageFormatDetector.Detect(packageStream);

        Stream inputStream = packageStream;
        GZipInputStream? gzipStream = null;

        switch (format)
        {
            case UnityPackageFormat.GzipTar:
                gzipStream = new GZipInputStream(packageStream);
                inputStream = gzipStream;
                break;
            case UnityPackageFormat.Tar:
                inputStream = packageStream;
                break;
            case UnityPackageFormat.TooSmall:
                throw new InvalidDataException(
                    "The selected file is too small to be a valid .unitypackage. It may be incomplete or corrupted.");
            default:
                throw CreateInvalidFormatException(format, packagePath, correlationId);
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

        try
        {
            return session.Execute();
        }
        catch (GZipException ex)
        {
            LoggingService.LogError(
                $"Extraction failed: invalid gzip data (package may be corrupted) | path='{packagePath}' | correlationId={correlationId}",
                ex);

            throw new InvalidDataException(
                "The package appears to be corrupted (gzip checksum mismatch). Please download the .unitypackage again.",
                ex);
        }
        finally
        {
            gzipStream?.Dispose();
        }
    }

    private static InvalidDataException CreateInvalidFormatException(
        UnityPackageFormat format,
        string packagePath,
        string correlationId)
    {
        var detected = UnityPackageFormatDetector.Describe(format);

        LoggingService.LogError(
            $"ExtractInternal aborted: unsupported package format | path='{packagePath}' | detected='{detected}' | correlationId={correlationId}",
            forwardToWebhook: false);

        return new InvalidDataException(
            $"The selected file appears to be {detected}, not a Unity .unitypackage (gzipped TAR). Please select a valid .unitypackage file.");
    }
}