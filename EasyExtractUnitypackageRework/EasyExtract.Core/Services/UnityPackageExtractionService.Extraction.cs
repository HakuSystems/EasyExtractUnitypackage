using System.Formats.Tar;
using System.IO.Compression;
using EasyExtract.Core.Models;

namespace EasyExtract.Core.Services;

public sealed partial class UnityPackageExtractionService
{
    private async Task<UnityPackageExtractionResult> ExtractInternalAsync(
        string packagePath,
        string outputDirectory,
        UnityPackageExtractionOptions options,
        IProgress<UnityPackageExtractionProgress>? progress,
        CancellationToken cancellationToken,
        string correlationId)
    {
        _logger.LogInformation(
            $"ExtractInternal started | package='{packagePath}' | correlationId={correlationId}");

        await using var packageStream = new FileStream(packagePath, FileMode.Open, FileAccess.Read, FileShare.Read,
            4096, FileOptions.Asynchronous);
        var format = UnityPackageFormatDetector.Detect(packageStream);

        Stream inputStream = packageStream;
        GZipStream? gzipStream = null;

        switch (format)
        {
            case UnityPackageFormat.GzipTar:
                gzipStream = new GZipStream(packageStream, CompressionMode.Decompress);
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

        await using var tarReader = new TarReader(inputStream, true);

        var normalizedOutputDirectory = NormalizeOutputDirectory(outputDirectory);
        var limits = UnityPackageExtractionLimits.Normalize(options.Limits);
        using var temporaryDirectory = CreateTemporaryDirectory(options.TemporaryDirectory, correlationId, _logger);

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
            correlationId,
            _logger); // Inject Logger

        try
        {
            return await session.ExecuteAsync().ConfigureAwait(false);
        }
        catch (InvalidDataException ex)
        {
            _logger.LogError(
                $"Extraction failed: invalid package data | path='{packagePath}' | correlationId={correlationId}",
                ex);
            throw;
        }
        finally
        {
            if (gzipStream != null)
                await gzipStream.DisposeAsync().ConfigureAwait(false);
        }
    }

    private InvalidDataException CreateInvalidFormatException(
        UnityPackageFormat format,
        string packagePath,
        string correlationId)
    {
        var detected = UnityPackageFormatDetector.Describe(format);

        _logger.LogError(
            $"ExtractInternal aborted: unsupported package format | path='{packagePath}' | detected='{detected}' | correlationId={correlationId}");

        return new InvalidDataException(
            $"The selected file appears to be {detected}, not a Unity .unitypackage (gzipped TAR). Please select a valid .unitypackage file.");
    }
}