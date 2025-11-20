using System;
using System.IO;

namespace EasyExtractCrossPlatform.Utilities;

public static class NotificationMessageFormatter
{
    public static string BuildExtractionMessage(string packagePath, string outputDirectory, int assetsExtracted)
    {
        var fileName = TryGetFileName(packagePath);
        var assetDescriptor = BuildAssetDescriptor(assetsExtracted);
        var destinationDescriptor = string.IsNullOrWhiteSpace(outputDirectory)
            ? assetDescriptor
            : $"{assetDescriptor} to {outputDirectory}";

        var message = string.IsNullOrWhiteSpace(fileName)
            ? destinationDescriptor
            : $"{fileName}: {destinationDescriptor}";

        return NormalizeMessage(message);
    }

    public static string BuildAssetDescriptor(int assetsExtracted)
    {
        if (assetsExtracted <= 0)
            return "Extraction completed";

        return assetsExtracted == 1
            ? "1 asset extracted"
            : $"{assetsExtracted} assets extracted";
    }

    private static string TryGetFileName(string packagePath)
    {
        if (string.IsNullOrWhiteSpace(packagePath))
            return string.Empty;

        try
        {
            return Path.GetFileName(packagePath);
        }
        catch
        {
            return packagePath;
        }
    }

    private static string NormalizeMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return string.Empty;

        return message.ReplaceLineEndings(" ").Trim();
    }
}
