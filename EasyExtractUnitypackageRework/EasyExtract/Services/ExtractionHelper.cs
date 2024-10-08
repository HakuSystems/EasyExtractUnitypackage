namespace EasyExtract.Services;

public record ExtractionHelper
{
    /// <summary>
    ///     Gets the readable file size representation of the given size.
    /// </summary>
    /// <param name="size">The size to convert.</param>
    /// <returns>The readable file size.</returns>
    public static async Task<string> GetReadableFileSize(long size)
    {
        string[] sizes =
        [
            "B", "KB", "MB", "GB", "TB"
        ];
        var order = 0;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size = size / 1024;
        }

        return $"{size:0.##} {sizes[order]}";
    }

    public static async Task<int> GetTotalFileCount(string directory)
    {
        var count = Directory.GetFiles(directory, "*.*", SearchOption.AllDirectories).Length;
        return count;
    }

    public static async Task<int> GetTotalFolderCount(string directory)
    {
        var count = Directory.GetDirectories(directory, "*", SearchOption.AllDirectories).Length;
        return count;
    }

    public static async Task<int> GetTotalScriptCount(string directory)
    {
        var count = Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories).Length;
        return count;
    }

    public static async Task<int> GetTotalShaderCount(string directory)
    {
        var count = Directory.GetFiles(directory, "*.shader", SearchOption.AllDirectories).Length;
        return count;
    }

    public static async Task<int> GetTotalPrefabCount(string directory)
    {
        var count = Directory.GetFiles(directory, "*.prefab", SearchOption.AllDirectories).Length;
        return count;
    }

    public static async Task<int> GetTotal3DObjectCount(string directory)
    {
        var count = Directory.GetFiles(directory, "*.fbx", SearchOption.AllDirectories).Length;
        return count;
    }

    public static async Task<int> GetTotalImageCount(string directory)
    {
        var count = Directory.GetFiles(directory, "*.png", SearchOption.AllDirectories).Length +
                    Directory.GetFiles(directory, "*.jpg", SearchOption.AllDirectories).Length +
                    Directory.GetFiles(directory, "*.jpeg", SearchOption.AllDirectories).Length +
                    Directory.GetFiles(directory, "*.tga", SearchOption.AllDirectories).Length +
                    Directory.GetFiles(directory, "*.psd", SearchOption.AllDirectories).Length;
        return count;
    }

    public static async Task<int> GetTotalAudioCount(string directory)
    {
        var count = Directory.GetFiles(directory, "*.mp3", SearchOption.AllDirectories).Length +
                    Directory.GetFiles(directory, "*.wav", SearchOption.AllDirectories).Length +
                    Directory.GetFiles(directory, "*.ogg", SearchOption.AllDirectories).Length;
        return count;
    }

    public static async Task<int> GetTotalAnimationCount(string directory)
    {
        var count = Directory.GetFiles(directory, "*.anim", SearchOption.AllDirectories).Length;
        return count;
    }

    public static async Task<int> GetTotalSceneCount(string directory)
    {
        var count = Directory.GetFiles(directory, "*.unity", SearchOption.AllDirectories).Length;
        return count;
    }

    public static async Task<int> GetTotalMaterialCount(string directory)
    {
        var count = Directory.GetFiles(directory, "*.mat", SearchOption.AllDirectories).Length;
        return count;
    }

    public static async Task<int> GetTotalAssetCount(string directory)
    {
        var count = Directory.GetFiles(directory, "*.asset", SearchOption.AllDirectories).Length;
        return count;
    }

    public static async Task<int> GetTotalControllerCount(string directory)
    {
        var count = Directory.GetFiles(directory, "*.controller", SearchOption.AllDirectories).Length;
        return count;
    }

    public static async Task<int> GetTotalFontCount(string directory)
    {
        var count = Directory.GetFiles(directory, "*.ttf", SearchOption.AllDirectories).Length +
                    Directory.GetFiles(directory, "*.otf", SearchOption.AllDirectories).Length;
        return count;
    }

    public static async Task<int> GetTotalConfigurationCount(string directory)
    {
        var count = Directory.GetFiles(directory, "*.meta", SearchOption.AllDirectories).Length +
                    Directory.GetFiles(directory, "*.json", SearchOption.AllDirectories).Length +
                    Directory.GetFiles(directory, "*.xml", SearchOption.AllDirectories).Length +
                    Directory.GetFiles(directory, "*.yaml", SearchOption.AllDirectories).Length;
        return count;
    }

    public static async Task<int> GetTotalDataCount(string directory)
    {
        var count = Directory.GetFiles(directory, "*.json", SearchOption.AllDirectories).Length +
                    Directory.GetFiles(directory, "*.xml", SearchOption.AllDirectories).Length +
                    Directory.GetFiles(directory, "*.yaml", SearchOption.AllDirectories).Length;
        return count;
    }

    /// <summary>
    ///     Gets the symbol icon based on the file extension.
    /// </summary>
    /// <param name="extension">The file extension.</param>
    /// <returns>The symbol icon.</returns>
    public static async Task<string> GetSymbolByExtension(string extension)
    {
        var symbol = extension switch
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

        return symbol;
    }

    /// <summary>
    ///     Gets the category of a file based on its extension.
    /// </summary>
    /// <param name="extension">The file extension.</param>
    /// <returns>The category of the file.</returns>
    public static async Task<string?> GetCategoryByExtension(string extension)
    {
        const string? img = "Image";
        var category = extension switch
        {
            ".cs" => "Script",
            ".shader" => "Shader",
            ".prefab" => "Prefab",
            ".fbx" => "3D Object",
            ".png" => img,
            ".jpg" => img,
            ".jpeg" => img,
            ".tga" => img,
            ".psd" => img,
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

        return category;
    }

    public static async Task<int> GetMalicousDiscordWebhookCount(string directory)
    {
        var count = 0;
        var codeFiles = Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories).ToList();

        foreach (var codeFile in codeFiles)
        {
            var isMalicious = false;
            var lines = await File.ReadAllLinesAsync(codeFile);

            foreach (var line in lines)
                if (await MaliciousCodeDetector.StartDiscordWebhookScanAsync(line))
                {
                    isMalicious = true;
                    break;
                }

            if (isMalicious) count++;
        }


        return count;
    }

    public static async Task<int> GetTotalLinkDetectionCount(string directory)
    {
        var count = 0;
        var codeFiles = Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories).ToList();

        foreach (var codeFile in codeFiles)
        {
            var isMalicious = false;
            var lines = await File.ReadAllLinesAsync(codeFile);

            foreach (var line in lines)
                if (await MaliciousCodeDetector.StartLinkDetectionAsync(line))
                {
                    isMalicious = true;
                    break;
                }

            if (isMalicious) count++;
        }


        return count;
    }

    #region Icons

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

    #endregion
}