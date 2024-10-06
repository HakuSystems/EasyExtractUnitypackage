using EasyExtract.Config;
using EasyExtract.Utilities;

namespace EasyExtract.Extraction;

public class ExtractionHelper
{
    private readonly BetterLogger _logger = new();

    /// <summary>
    ///     Gets the readable file size representation of the given size.
    /// </summary>
    /// <param name="size">The size to convert.</param>
    /// <returns>The readable file size.</returns>
    public async Task<string> GetReadableFileSize(long size)
    {
        string[] sizes =
        {
            "B", "KB", "MB", "GB", "TB"
        };
        var order = 0;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size = size / 1024;
        }

        await _logger.LogAsync($"Converted file size: {size:0.##} {sizes[order]}", "ExtractionHelper.cs",
            Importance.Info); // Log file size conversion
        return $"{size:0.##} {sizes[order]}";
    }

    public async Task<int> GetTotalFileCount(string directory)
    {
        var count = Directory.GetFiles(directory, "*.*", SearchOption.AllDirectories).Length;
        await _logger.LogAsync($"Total file count in directory '{directory}': {count}", "ExtractionHelper.cs",
            Importance.Info); // Log total file count
        return count;
    }

    public async Task<int> GetTotalFolderCount(string directory)
    {
        var count = Directory.GetDirectories(directory, "*", SearchOption.AllDirectories).Length;
        await _logger.LogAsync($"Total folder count in directory '{directory}': {count}", "ExtractionHelper.cs",
            Importance.Info); // Log total folder count
        return count;
    }

    public async Task<int> GetTotalScriptCount(string directory)
    {
        var count = Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories).Length;
        await _logger.LogAsync($"Total script count in directory '{directory}': {count}", "ExtractionHelper.cs",
            Importance.Info); // Log total script count
        return count;
    }

    public async Task<int> GetTotalShaderCount(string directory)
    {
        var count = Directory.GetFiles(directory, "*.shader", SearchOption.AllDirectories).Length;
        await _logger.LogAsync($"Total shader count in directory '{directory}': {count}", "ExtractionHelper.cs",
            Importance.Info); // Log total shader count
        return count;
    }

    public async Task<int> GetTotalPrefabCount(string directory)
    {
        var count = Directory.GetFiles(directory, "*.prefab", SearchOption.AllDirectories).Length;
        await _logger.LogAsync($"Total prefab count in directory '{directory}': {count}", "ExtractionHelper.cs",
            Importance.Info); // Log total prefab count
        return count;
    }

    public async Task<int> GetTotal3DObjectCount(string directory)
    {
        var count = Directory.GetFiles(directory, "*.fbx", SearchOption.AllDirectories).Length;
        await _logger.LogAsync($"Total 3D object count in directory '{directory}': {count}", "ExtractionHelper.cs",
            Importance.Info); // Log total 3D object count
        return count;
    }

    public async Task<int> GetTotalImageCount(string directory)
    {
        var count = Directory.GetFiles(directory, "*.png", SearchOption.AllDirectories).Length +
                    Directory.GetFiles(directory, "*.jpg", SearchOption.AllDirectories).Length +
                    Directory.GetFiles(directory, "*.jpeg", SearchOption.AllDirectories).Length +
                    Directory.GetFiles(directory, "*.tga", SearchOption.AllDirectories).Length +
                    Directory.GetFiles(directory, "*.psd", SearchOption.AllDirectories).Length;
        await _logger.LogAsync($"Total image count in directory '{directory}': {count}", "ExtractionHelper.cs",
            Importance.Info); // Log total image count
        return count;
    }

    public async Task<int> GetTotalAudioCount(string directory)
    {
        var count = Directory.GetFiles(directory, "*.mp3", SearchOption.AllDirectories).Length +
                    Directory.GetFiles(directory, "*.wav", SearchOption.AllDirectories).Length +
                    Directory.GetFiles(directory, "*.ogg", SearchOption.AllDirectories).Length;
        await _logger.LogAsync($"Total audio count in directory '{directory}': {count}", "ExtractionHelper.cs",
            Importance.Info); // Log total audio count
        return count;
    }

    public async Task<int> GetTotalAnimationCount(string directory)
    {
        var count = Directory.GetFiles(directory, "*.anim", SearchOption.AllDirectories).Length;
        await _logger.LogAsync($"Total animation count in directory '{directory}': {count}", "ExtractionHelper.cs",
            Importance.Info); // Log total animation count
        return count;
    }

    public async Task<int> GetTotalSceneCount(string directory)
    {
        var count = Directory.GetFiles(directory, "*.unity", SearchOption.AllDirectories).Length;
        await _logger.LogAsync($"Total scene count in directory '{directory}': {count}", "ExtractionHelper.cs",
            Importance.Info); // Log total scene count
        return count;
    }

    public async Task<int> GetTotalMaterialCount(string directory)
    {
        var count = Directory.GetFiles(directory, "*.mat", SearchOption.AllDirectories).Length;
        await _logger.LogAsync($"Total material count in directory '{directory}': {count}", "ExtractionHelper.cs",
            Importance.Info); // Log total material count
        return count;
    }

    public async Task<int> GetTotalAssetCount(string directory)
    {
        var count = Directory.GetFiles(directory, "*.asset", SearchOption.AllDirectories).Length;
        await _logger.LogAsync($"Total asset count in directory '{directory}': {count}", "ExtractionHelper.cs",
            Importance.Info); // Log total asset count
        return count;
    }

    public async Task<int> GetTotalControllerCount(string directory)
    {
        var count = Directory.GetFiles(directory, "*.controller", SearchOption.AllDirectories).Length;
        await _logger.LogAsync($"Total controller count in directory '{directory}': {count}", "ExtractionHelper.cs",
            Importance.Info); // Log total controller count
        return count;
    }

    public async Task<int> GetTotalFontCount(string directory)
    {
        var count = Directory.GetFiles(directory, "*.ttf", SearchOption.AllDirectories).Length +
                    Directory.GetFiles(directory, "*.otf", SearchOption.AllDirectories).Length;
        await _logger.LogAsync($"Total font count in directory '{directory}': {count}", "ExtractionHelper.cs",
            Importance.Info); // Log total font count
        return count;
    }

    public async Task<int> GetTotalConfigurationCount(string directory)
    {
        var count = Directory.GetFiles(directory, "*.meta", SearchOption.AllDirectories).Length +
                    Directory.GetFiles(directory, "*.json", SearchOption.AllDirectories).Length +
                    Directory.GetFiles(directory, "*.xml", SearchOption.AllDirectories).Length +
                    Directory.GetFiles(directory, "*.yaml", SearchOption.AllDirectories).Length;
        await _logger.LogAsync($"Total configuration count in directory '{directory}': {count}", "ExtractionHelper.cs",
            Importance.Info); // Log total configuration count
        return count;
    }

    public async Task<int> GetTotalDataCount(string directory)
    {
        var count = Directory.GetFiles(directory, "*.json", SearchOption.AllDirectories).Length +
                    Directory.GetFiles(directory, "*.xml", SearchOption.AllDirectories).Length +
                    Directory.GetFiles(directory, "*.yaml", SearchOption.AllDirectories).Length;
        await _logger.LogAsync($"Total data count in directory '{directory}': {count}", "ExtractionHelper.cs",
            Importance.Info); // Log total data count
        return count;
    }

    /// <summary>
    ///     Gets the symbol icon based on the file extension.
    /// </summary>
    /// <param name="extension">The file extension.</param>
    /// <returns>The symbol icon.</returns>
    public async Task<string> GetSymbolByExtension(string extension)
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

        await _logger.LogAsync($"Symbol for extension '{extension}': {symbol}", "ExtractionHelper.cs",
            Importance.Info); // Log symbol by extension
        return symbol;
    }

    /// <summary>
    ///     Gets the category of a file based on its extension.
    /// </summary>
    /// <param name="extension">The file extension.</param>
    /// <returns>The category of the file.</returns>
    public async Task<string> GetCategoryByExtension(string extension)
    {
        var category = extension switch
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

        await _logger.LogAsync($"Category for extension '{extension}': {category}", "ExtractionHelper.cs",
            Importance.Info); // Log category by extension
        return category;
    }

    public async Task<int> GetMalicousDiscordWebhookCount(string directory)
    {
        var count = 0;
        var codeFiles = Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories).ToList();
        var maliciousCodeDetector = new MaliciousCodeDetector();

        foreach (var codeFile in codeFiles)
        {
            var isMalicious = false;
            var lines = await File.ReadAllLinesAsync(codeFile);

            foreach (var line in lines)
                if (await maliciousCodeDetector.StartDiscordWebhookScanAsync(line))
                {
                    isMalicious = true;
                    break;
                }

            if (isMalicious) count++;
        }

        await _logger.LogAsync($"Total malicious code count in directory '{directory}': {count}", "ExtractionHelper.cs",
            Importance.Info); // Log total malicious code count

        return count;
    }

    public async Task<int> GetTotalLinkDetectionCount(string directory)
    {
        var count = 0;
        var codeFiles = Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories).ToList();
        var maliciousCodeDetector = new MaliciousCodeDetector();

        foreach (var codeFile in codeFiles)
        {
            var isMalicious = false;
            var lines = await File.ReadAllLinesAsync(codeFile);

            foreach (var line in lines)
                if (await maliciousCodeDetector.StartLinkDetectionAsync(line))
                {
                    isMalicious = true;
                    break;
                }

            if (isMalicious) count++;
        }

        await _logger.LogAsync($"Total link detection count in directory '{directory}': {count}", "ExtractionHelper.cs",
            Importance.Info); // Log total link detection count

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