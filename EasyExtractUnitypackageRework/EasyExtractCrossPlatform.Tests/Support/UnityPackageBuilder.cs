using System.Formats.Tar;
using System.IO.Compression;
using System.Text;

namespace EasyExtractCrossPlatform.Tests.Support;

/// <summary>
///     Writes throwaway .unitypackage fixtures: a gzip'd tar where every asset lives under its own
///     guid folder as an "asset" plus "pathname" pair, which is the layout Unity produces.
/// </summary>
internal sealed class UnityPackageBuilder
{
    private readonly List<(string AssetPath, byte[] Content)> _assets = [];

    public UnityPackageBuilder WithAsset(string assetPath, byte[] content)
    {
        _assets.Add((assetPath, content));
        return this;
    }

    public UnityPackageBuilder WithAsset(string assetPath, string content)
    {
        return WithAsset(assetPath, Encoding.UTF8.GetBytes(content));
    }

    public async Task<string> WriteAsync(string directory, string fileName)
    {
        Directory.CreateDirectory(directory);
        var packagePath = Path.Combine(directory, fileName);

        await using var fileStream =
            new FileStream(packagePath, FileMode.Create, FileAccess.Write, FileShare.None);
        await using var gzipStream = new GZipStream(fileStream, CompressionMode.Compress);
        await using var writer = new TarWriter(gzipStream, TarEntryFormat.Pax);

        foreach (var (assetPath, content) in _assets)
        {
            var guid = Guid.NewGuid().ToString("N");

            await WriteEntryAsync(writer, $"{guid}/pathname", Encoding.UTF8.GetBytes(assetPath));
            await WriteEntryAsync(writer, $"{guid}/asset", content);
        }

        return packagePath;
    }

    private static async Task WriteEntryAsync(TarWriter writer, string entryName, byte[] data)
    {
        using var dataStream = new MemoryStream(data, false);
        var entry = new PaxTarEntry(TarEntryType.RegularFile, entryName)
        {
            DataStream = dataStream
        };

        await writer.WriteEntryAsync(entry);
    }
}
