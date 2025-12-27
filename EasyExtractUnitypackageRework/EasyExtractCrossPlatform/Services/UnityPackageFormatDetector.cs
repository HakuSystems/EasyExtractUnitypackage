namespace EasyExtractCrossPlatform.Services;

internal enum UnityPackageFormat
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

internal static class UnityPackageFormatDetector
{
    private const int MaxHeaderBytes = 512;

    public static UnityPackageFormat Detect(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var headerLength = stream.Length > 0
            ? (int)Math.Min(stream.Length, MaxHeaderBytes)
            : 0;

        var headerBuffer = new byte[headerLength];
        var bytesRead = stream.Read(headerBuffer, 0, headerBuffer.Length);
        stream.Position = 0;

        return Detect(headerBuffer.AsSpan(0, bytesRead));
    }

    public static UnityPackageFormat Detect(ReadOnlySpan<byte> header)
    {
        if (header.Length >= 2 && header[0] == 0x1F && header[1] == 0x8B)
            return UnityPackageFormat.GzipTar;

        if (LooksLikeZip(header))
            return UnityPackageFormat.Zip;

        if (LooksLikeRar(header))
            return UnityPackageFormat.Rar;

        if (LooksLikeSevenZip(header))
            return UnityPackageFormat.SevenZip;

        if (LooksLikeUnityFs(header))
            return UnityPackageFormat.UnityFs;

        if (LooksLikeTar(header))
            return UnityPackageFormat.Tar;

        return header.Length < MaxHeaderBytes
            ? UnityPackageFormat.TooSmall
            : UnityPackageFormat.Unknown;
    }

    public static string Describe(UnityPackageFormat format)
    {
        return format switch
        {
            UnityPackageFormat.Zip => "a ZIP archive",
            UnityPackageFormat.Rar => "a RAR archive",
            UnityPackageFormat.SevenZip => "a 7z archive",
            UnityPackageFormat.UnityFs => "a UnityFS asset bundle",
            UnityPackageFormat.TooSmall => "an incomplete file",
            _ => "an unsupported format"
        };
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
}