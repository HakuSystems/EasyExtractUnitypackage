using System.IO;
using EasyExtract.Config;

namespace EasyExtract.Extraction;

public class ExtractedFileHandler
{
    private static string LastExtractedPath { get; } = ConfigModel.LastExtractedPath;

    public static void PutExtractedFileToConfig()
    {
        var extractedUnitypackages = Directory.GetDirectories(LastExtractedPath);
        foreach (var unitypackage in extractedUnitypackages)
        {
            var extractedFiles = Directory.GetFiles(unitypackage, "*.*", SearchOption.AllDirectories);
            foreach (var file in extractedFiles)
            {
                var extractedFile = new ExtractedFiles
                {
                    FileName = Path.GetFileName(file),
                    FilePath = file,
                    Category = Path.GetExtension(file),
                    Extension = Path.GetExtension(file),
                    Size = new FileInfo(file).Length.ToString(),
                    ExtractedDate = File.GetCreationTime(file)
                };
            }
        }
    }
}