using System.IO;
using EasyExtract.Config;

namespace EasyExtract.Extraction;

public class ExtractionHelper
{
    private const string Code24 = "Code24";
    private const string Box24 = "Box24";
    private const string Cube24 = "Cube24";
    private const string Image24 = "Image24";
    private const string MusicNote24 = "MusicNote216";
    private const string Video24 = "Video24";
    private const string Document24 = "Document24";
    private const string Paint24 = "PaintBrush24";
    private const string GameController24 = "XboxController24";
    private const string Text24 = "SlideText24";
    private const string Settings24 = "Settings24";

    /// <summary>
    ///     Gets the readable file size representation of the given size.
    /// </summary>
    /// <param name="size">The size to convert.</param>
    /// <returns>The readable file size.</returns>
    public static string GetReadableFileSize(long size)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        var order = 0;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size = size / 1024;
        }

        return $"{size:0.##} {sizes[order]}";
    }

    public static int GetTotalFileCount(string directory)
    {
        //BASED ON GetCategoryByExtension
        return Directory.GetFiles(directory, "*.*", SearchOption.AllDirectories).Length;
    }

    public static int GetTotalFolderCount(string directory)
    {
        //BASED ON GetCategoryByExtension
        return Directory.GetDirectories(directory, "*", SearchOption.AllDirectories).Length;
    }

    public static int GetTotalScriptCount(string directory)
    {
        //BASED ON GetCategoryByExtension
        return Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories).Length;
    }

    public static int GetTotalShaderCount(string directory)
    {
        //BASED ON GetCategoryByExtension
        return Directory.GetFiles(directory, "*.shader", SearchOption.AllDirectories).Length;
    }

    public static int GetTotalPrefabCount(string directory)
    {
        //BASED ON GetCategoryByExtension
        return Directory.GetFiles(directory, "*.prefab", SearchOption.AllDirectories).Length;
    }

    public static int GetTotal3DObjectCount(string directory)
    {
        //BASED ON GetCategoryByExtension
        return Directory.GetFiles(directory, "*.fbx", SearchOption.AllDirectories).Length;
    }

    public static int GetTotalImageCount(string directory)
    {
        //BASED ON GetCategoryByExtension
        return Directory.GetFiles(directory, "*.png", SearchOption.AllDirectories).Length +
               Directory.GetFiles(directory, "*.jpg", SearchOption.AllDirectories).Length +
               Directory.GetFiles(directory, "*.jpeg", SearchOption.AllDirectories).Length +
               Directory.GetFiles(directory, "*.tga", SearchOption.AllDirectories).Length +
               Directory.GetFiles(directory, "*.psd", SearchOption.AllDirectories).Length;
    }

    public static int GetTotalAudioCount(string directory)
    {
        //BASED ON GetCategoryByExtension
        return Directory.GetFiles(directory, "*.mp3", SearchOption.AllDirectories).Length +
               Directory.GetFiles(directory, "*.wav", SearchOption.AllDirectories).Length +
               Directory.GetFiles(directory, "*.ogg", SearchOption.AllDirectories).Length;
    }

    public static int GetTotalAnimationCount(string directory)
    {
        //BASED ON GetCategoryByExtension
        return Directory.GetFiles(directory, "*.anim", SearchOption.AllDirectories).Length;
    }

    public static int GetTotalSceneCount(string directory)
    {
        //BASED ON GetCategoryByExtension
        return Directory.GetFiles(directory, "*.unity", SearchOption.AllDirectories).Length;
    }

    public static int GetTotalMaterialCount(string directory)
    {
        //BASED ON GetCategoryByExtension
        return Directory.GetFiles(directory, "*.mat", SearchOption.AllDirectories).Length;
    }

    public static int GetTotalAssetCount(string directory)
    {
        //BASED ON GetCategoryByExtension
        return Directory.GetFiles(directory, "*.asset", SearchOption.AllDirectories).Length;
    }

    public static int GetTotalControllerCount(string directory)
    {
        //BASED ON GetCategoryByExtension
        return Directory.GetFiles(directory, "*.controller", SearchOption.AllDirectories).Length;
    }

    public static int GetTotalFontCount(string directory)
    {
        //BASED ON GetCategoryByExtension
        return Directory.GetFiles(directory, "*.ttf", SearchOption.AllDirectories).Length +
               Directory.GetFiles(directory, "*.otf", SearchOption.AllDirectories).Length;
    }

    public static int GetTotalConfigurationCount(string directory)
    {
        //BASED ON GetCategoryByExtension
        return Directory.GetFiles(directory, "*.meta", SearchOption.AllDirectories).Length +
               Directory.GetFiles(directory, "*.json", SearchOption.AllDirectories).Length +
               Directory.GetFiles(directory, "*.xml", SearchOption.AllDirectories).Length +
               Directory.GetFiles(directory, "*.yaml", SearchOption.AllDirectories).Length;
    }

    public static int GetTotalDataCount(string directory)
    {
        //BASED ON GetCategoryByExtension
        return Directory.GetFiles(directory, "*.json", SearchOption.AllDirectories).Length +
               Directory.GetFiles(directory, "*.xml", SearchOption.AllDirectories).Length +
               Directory.GetFiles(directory, "*.yaml", SearchOption.AllDirectories).Length;
    }

    /// <summary>
    ///     Gets the symbol icon based on the file extension.
    /// </summary>
    /// <param name="extension">The file extension.</param>
    /// <returns>The symbol icon.</returns>
    public static string GetSymbolByExtension(string extension)
    {
        return extension switch
        {
            ".cs" => Code24,
            ".shader" => Code24,
            ".prefab" => Box24,
            ".fbx" => Cube24,
            ".png" => Image24,
            ".jpg" => Image24,
            ".jpeg" => Image24,
            ".tga" => Image24,
            ".psd" => Image24,
            ".mp3" => MusicNote24,
            ".wav" => MusicNote24,
            ".ogg" => MusicNote24,
            ".anim" => Video24,
            ".unity" => Document24,
            ".mat" => Paint24,
            ".asset" => Box24,
            ".controller" => GameController24,
            ".ttf" => Text24,
            ".otf" => Text24,
            ".meta" => Settings24,
            ".json" => Settings24,
            ".xml" => Settings24,
            ".yaml" => Settings24,
            _ => Document24
        };
    }

    /// <summary>
    ///     Gets the category of a file based on its extension.
    /// </summary>
    /// <param name="extension">The file extension.</param>
    /// <returns>The category of the file.</returns>
    public static string GetCategoryByExtension(string extension)
    {
        return extension switch
        {
            ".cs" => "Script",
            ".shader" => "Shader",
            ".prefab" => "Prefab",
            ".fbx" => "3D Object",
            ".png" => "Image",
            ".jpg" => "Image",
            ".jpeg" => "Image",
            ".tga" => "Image",
            ".psd" => "Image",
            ".mp3" => "Audio",
            ".wav" => "Audio",
            ".ogg" => "Audio",
            ".anim" => "Animation",
            ".unity" => "Scene",
            ".mat" => "Material",
            ".asset" => "Asset",
            ".controller" => "Controller",
            ".ttf" => "Font",
            ".otf" => "Font",
            ".meta" => "Configuration",
            ".json" => "Data",
            ".xml" => "Data",
            ".yaml" => "Data",
            _ => "Document"
        };
    }

    /// <summary>
    ///     Changes the details string of an extracted Unitypackage.
    /// </summary>
    /// <param name="unitypackage">The extracted Unitypackage model.</param>
    public static void ChangeUnitypackageDetailsString(ExtractedUnitypackageModel unitypackage)
    {
        unitypackage.UnitypackageDetails = $"Total Files: {unitypackage.UnitypackageTotalFileCount:N2} | " +
                                           $"Total Folders: {unitypackage.UnitypackageTotalFolderCount:N2} | " +
                                           $"Total Scripts: {unitypackage.UnitypackageTotalScriptCount:N2} | " +
                                           $"Total Shaders: {unitypackage.UnitypackageTotalShaderCount:N2} | " +
                                           $"Total Prefabs: {unitypackage.UnitypackageTotalPrefabCount:N2} | " +
                                           $"Total 3D Objects: {unitypackage.UnitypackageTotal3DObjectCount:N2} | " +
                                           $"Total Images: {unitypackage.UnitypackageTotalImageCount:N2} | " +
                                           $"Total Audios: {unitypackage.UnitypackageTotalAudioCount:N2} | " +
                                           $"Total Animations: {unitypackage.UnitypackageTotalAnimationCount:N2} | " +
                                           $"Total Scenes: {unitypackage.UnitypackageTotalSceneCount:N2} | " +
                                           $"Total Materials: {unitypackage.UnitypackageTotalMaterialCount:N2} | " +
                                           $"Total Assets: {unitypackage.UnitypackageTotalAssetCount:N2} | " +
                                           $"Total Controllers: {unitypackage.UnitypackageTotalControllerCount:N2} | " +
                                           $"Total Fonts: {unitypackage.UnitypackageTotalFontCount:N2} | " +
                                           $"Total Configurations: {unitypackage.UnitypackageTotalConfigurationCount:N2} | " +
                                           $"Total Data: {unitypackage.UnitypackageTotalDataCount:N2}";

        // Each number in the string is formatted to match the following format: 0.00
        // For example: 1000 -> 1,000.00 and 1000000 -> 1,000,000.00
    }
}