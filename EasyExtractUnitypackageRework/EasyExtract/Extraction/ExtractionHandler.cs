using System.IO;
using System.IO.Compression;
using EasyExtract.Config;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace EasyExtract.Extraction;

public static class ExtractionHandler
{
    private static string LastExtractedPath { get; } = ConfigModel.LastExtractedPath;
    private static string EasyExtractTempPath { get; } = ConfigModel.DefaultTempPath;

    public static async Task<bool> ExtractUnitypackage(SearchEverythingModel unitypackage)
    {
        var tempFolder = Path.Combine(EasyExtractTempPath, unitypackage.UnityPackageName);
        var targetFolder = Path.Combine(LastExtractedPath, unitypackage.UnityPackageName);
        if (Directory.Exists(tempFolder))
            Directory.Delete(tempFolder, true);
        if (Directory.Exists(targetFolder))
            Directory.Delete(targetFolder, true);

        Directory.CreateDirectory(tempFolder);
        Directory.CreateDirectory(targetFolder);

        await Task.Run(() =>
        {
            using Stream inStream = File.OpenRead(unitypackage.UnityPackagePath);
            using Stream gzipStream = new GZipStream(inStream, CompressionMode.Decompress);

            var memoryStream = new MemoryStream();
            gzipStream.CopyTo(memoryStream);
            memoryStream.Position = 0; // Reset position to start

            using var archive = ArchiveFactory.Open(memoryStream);
            foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
                entry.WriteToDirectory(tempFolder, new ExtractionOptions { ExtractFullPath = true, Overwrite = true });
        });

        return true;
    }
}