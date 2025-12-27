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

        var headerLength = (int)Math.Min(packageStream.Length, 512);
        var headerBuffer = new byte[headerLength];
        var bytesRead = packageStream.Read(headerBuffer, 0, headerLength);
        packageStream.Position = 0; // Reset position

        var format = DetectPackageFormat(headerBuffer.AsSpan(0, bytesRead));

        Stream inputStream = packageStream;
        GZipInputStream? gzipStream = null;

        switch (format)
        {
            case PackageFormat.GzipTar:
                gzipStream = new GZipInputStream(packageStream);
                inputStream = gzipStream;
                break;
            case PackageFormat.Tar:
                inputStream = packageStream;
                break;
            case PackageFormat.Zip:
            case PackageFormat.Rar:
            case PackageFormat.SevenZip:
            case PackageFormat.UnityFs:
                throw CreateInvalidFormatException(format, packagePath, correlationId);
            case PackageFormat.TooSmall:
                throw new InvalidDataException(
                    "The selected file is too small to be a valid .unitypackage. It may be incomplete or corrupted.");
            default:
                throw new InvalidDataException(
                    "The selected file is not a valid Unity .unitypackage (expected a gzipped TAR archive).");
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

    private static InvalidDataException CreateInvalidFormatException(
        PackageFormat format,
        string packagePath,
        string correlationId)
    {
        var detected = format switch
        {
            PackageFormat.Zip => "ZIP archive",
            PackageFormat.Rar => "RAR archive",
            PackageFormat.SevenZip => "7z archive",
            PackageFormat.UnityFs => "UnityFS asset bundle",
            _ => "an unsupported format"
        };

        LoggingService.LogError(
            $"ExtractInternal aborted: unsupported package format | path='{packagePath}' | detected='{detected}' | correlationId={correlationId}");

        return new InvalidDataException(
            $"The selected file appears to be a {detected}, not a Unity .unitypackage (gzipped TAR). Please select a valid .unitypackage file.");
    }

    private static PackageFormat DetectPackageFormat(ReadOnlySpan<byte> header)
    {
        if (header.Length >= 2 && header[0] == 0x1F && header[1] == 0x8B)
            return PackageFormat.GzipTar;

        if (LooksLikeZip(header))
            return PackageFormat.Zip;

        if (LooksLikeRar(header))
            return PackageFormat.Rar;

        if (LooksLikeSevenZip(header))
            return PackageFormat.SevenZip;

        if (LooksLikeUnityFs(header))
            return PackageFormat.UnityFs;

        if (LooksLikeTar(header))
            return PackageFormat.Tar;

        return header.Length < 512 ? PackageFormat.TooSmall : PackageFormat.Unknown;
    }

    private static bool LooksLikeZip(ReadOnlySpan<byte> header)
    {
        return header.Length >= 4 &&
               header[0] == 0x50 &&
               header[1] == 0x4B &&
               (header[2] == 0x03 || header[2] == 0x05 || header[2] == 0x07) &&
               (header[3] == 0x04 || header[3] == 0x06 || header[3] == 0x08);
    }

    private static bool LooksLikeRar(ReadOnlySpan<byte> header)
    {
        return header.Length >= 7 &&
               header[0] == 0x52 &&
               header[1] == 0x61 &&
               header[2] == 0x72 &&
               header[3] == 0x21 &&
               header[4] == 0x1A &&
               header[5] == 0x07 &&
               (header[6] == 0x00 || header[6] == 0x01);
    }

    private static bool LooksLikeSevenZip(ReadOnlySpan<byte> header)
    {
        return header.Length >= 6 &&
               header[0] == 0x37 &&
               header[1] == 0x7A &&
               header[2] == 0xBC &&
               header[3] == 0xAF &&
               header[4] == 0x27 &&
               header[5] == 0x1C;
    }

    private static bool LooksLikeUnityFs(ReadOnlySpan<byte> header)
    {
        return header.Length >= 6 &&
               header[0] == (byte)'U' &&
               header[1] == (byte)'n' &&
               header[2] == (byte)'i' &&
               header[3] == (byte)'t' &&
               header[4] == (byte)'y' &&
               header[5] == (byte)'F';
    }

    private static bool LooksLikeTar(ReadOnlySpan<byte> header)
    {
        if (header.Length < 262)
            return false;

        // POSIX ustar signature at offset 257
        return header[257] == (byte)'u' &&
               header[258] == (byte)'s' &&
               header[259] == (byte)'t' &&
               header[260] == (byte)'a' &&
               header[261] == (byte)'r';
    }

    private enum PackageFormat
    {
        Unknown,
        GzipTar,
        Tar,
        Zip,
        Rar,
        SevenZip,
        UnityFs,
        TooSmall
    }
}