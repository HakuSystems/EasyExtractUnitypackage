using System.Text;

namespace EasyExtract.Services;

public record ExtractionHelper
{
    public static readonly HashSet<string> ValidExtensions =
        new(StringComparer.OrdinalIgnoreCase) // Credits oguzhan_sparklegames
        {
            ".cs", ".shader", ".prefab", ".fbx", ".png", ".jpg", ".jpeg", ".tga", ".psd",
            ".mp3", ".wav", ".ogg", ".anim", ".unity", ".mat", ".asset", ".controller",
            ".ttf", ".otf", ".meta", ".json", ".xml", ".yaml",
            ".blend", ".dll", ".txt", ".pdf", ".docx", ".xlsx", ".html", ".js", ".py"
        };

    public static async Task<long> GetTotalSizeInBytesAsync(string directory)
    {
        var files = Directory.GetFiles(directory, "*.*", SearchOption.AllDirectories);
        var totalBytes = files.Sum(file => new FileInfo(file).Length);
        return totalBytes;
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

        return symbol;
    }

    /// <summary>
    ///     Gets the category of a file based on its extension.
    /// </summary>
    /// <param name="extension">The file extension.</param>
    /// <returns>The category of the file.</returns>
    public async Task<string?> GetCategoryByExtension(string extension)
    {
        const string? img = "Image";
        const string? audio = "Audio";
        const string? animation = "Animation";
        const string? scene = "Scene";
        const string? material = "Material";
        const string? asset = "Asset";
        const string? controller = "Controller";
        const string? font = "Font";
        const string? configuration = "Configuration";
        const string? data = "Data";
        const string? script = "Script";
        const string? shader = "Shader";
        const string? prefab = "Prefab";
        const string? unknown = "Unknown";
        const string? model = "3D Object";
        var category = extension switch
        {
            ".cs" => script,
            ".shader" => shader,
            ".prefab" => prefab,
            ".fbx" => model,
            ".png" => img,
            ".jpg" => img,
            ".jpeg" => img,
            ".tga" => img,
            ".psd" => img,
            ".mp3" => audio,
            ".wav" => audio,
            ".ogg" => audio,
            ".anim" => animation,
            ".unity" => scene,
            ".mat" => material,
            ".asset" => asset,
            ".controller" => controller,
            ".ttf" => font,
            ".otf" => font,
            ".meta" => configuration,
            ".json" => data,
            ".xml" => data,
            ".yaml" => data,
            _ => unknown
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

    // Encryption detection (dlls) START
    public async Task<bool> IsEncryptedDll(string filePath)
    {
        var fileBytes = await File.ReadAllBytesAsync(filePath);

        // Check for standard PE header (MZ and PE signatures)
        if (fileBytes.Length < 0x200) return true; // Too small to be valid DLL

        // Check "MZ" header at start of file
        if (fileBytes[0] != 'M' || fileBytes[1] != 'Z')
            return true; // Missing standard MZ header

        // PE header offset is located at 0x3C
        var peHeaderOffset = BitConverter.ToInt32(fileBytes, 0x3C);
        if (peHeaderOffset + 4 > fileBytes.Length)
            return true; // Invalid PE header offset

        // Check "PE\0\0" signature
        if (fileBytes[peHeaderOffset] != 'P' || fileBytes[peHeaderOffset + 1] != 'E' ||
            fileBytes[peHeaderOffset + 2] != 0 || fileBytes[peHeaderOffset + 3] != 0)
            return true; // Missing PE signature indicates encrypted or invalid DLL

        // Calculate entropy to detect possible encryption
        var entropy = CalculateEntropy(fileBytes);
        if (entropy > 7.5)
            return true; // High entropy strongly suggests encryption

        // Check sections for suspicious names indicating encryption or obfuscation
        if (HasSuspiciousSectionNames(fileBytes, peHeaderOffset))
            return true;

        return false;
    }

    private double CalculateEntropy(byte[] data)
    {
        var frequencies = new int[256];
        foreach (var b in data)
            frequencies[b]++;

        double entropy = 0;
        double len = data.Length;

        foreach (var freq in frequencies)
        {
            if (freq == 0) continue;
            var prob = freq / len;
            entropy += -prob * Math.Log2(prob);
        }

        return entropy;
    }

    private bool HasSuspiciousSectionNames(byte[] fileBytes, int peHeaderOffset)
    {
        try
        {
            // Number of sections is a 2-byte value located at offset 6 from PE header
            int numberOfSections = BitConverter.ToInt16(fileBytes, peHeaderOffset + 6);
            // Section headers start 248 bytes (0xF8) after PE signature
            var sectionHeadersOffset = peHeaderOffset + 0xF8;

            string[] suspiciousNames = { ".enc", ".crypt", ".themida", ".vmp", ".upx", ".pack", ".petite" };

            for (var i = 0; i < numberOfSections; i++)
            {
                var sectionOffset = sectionHeadersOffset + 40 * i;
                if (sectionOffset + 8 > fileBytes.Length) break;

                var sectionName = Encoding.ASCII.GetString(fileBytes, sectionOffset, 8).TrimEnd('\0').ToLower();

                if (suspiciousNames.Any(name => sectionName.Contains(name)))
                    return true; // Found suspicious section name
            }
        }
        catch
        {
            return true; // Parsing error usually indicates something is wrong or encrypted
        }

        return false;
    }
    // Encryption detection (dlls) END

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