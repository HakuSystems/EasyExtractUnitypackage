namespace EasyExtract.Core.Utilities;

public static class FileExtensionNormalizer
{
    // Unitypackage extraction bug inflates extensions well beyond their expected length (~30+ chars).
    private const int MinimumCorruptedExtensionLength = 30;

    private static readonly Lazy<IReadOnlyList<string>> KnownExtensions =
        new(LoadExtensions, true);

    private static readonly IReadOnlyList<string> DefaultExtensions = new[]
    {
        ".scenewithbuildsettings",
        ".fusionweavertrigger",
        ".overridecontroller",
        ".physicsmaterial2d",
        ".noisehlsltemplate",
        ".physicmaterial",
        ".spriteatlasv2",
        ".rendertexture",
        ".inputactions",
        ".terrainlayer",
        ".fontsettings",
        ".shadergraph",
        ".spriteatlas",
        ".controller",
        ".cfxrshader",
        ".spritelib",
        ".xcprivacy",
        ".gradients",
        ".modulemap",
        ".lighting",
        ".giparams",
        ".playable",
        ".raytrace",
        ".afdesign",
        ".cubemap",
        ".compute",
        ".guiskin",
        ".defines",
        ".tpsheet",
        ".release",
        ".strings",
        ".prefab",
        ".colors",
        ".preset",
        ".asmdef",
        ".shader",
        ".pcache",
        ".signal",
        ".nuspec",
        ".ignore",
        ".readme",
        ".bundle",
        ".stgmat",
        ".tbpost",
        ".curves",
        ".srcaar",
        ".asmref",
        ".asset",
        ".unity",
        ".sbsar",
        ".mixer",
        ".cginc",
        ".bytes",
        ".solid",
        ".flare",
        ".jslib",
        ".props",
        ".nunit",
        ".dylib",
        ".fspro",
        ".plist",
        ".blend",
        ".debug",
        ".brush",
        ".anim",
        ".tiff",
        ".meta",
        ".json",
        ".html",
        ".xlsx",
        ".docx",
        ".mask",
        ".uxml",
        ".hlsl",
        ".mesh",
        ".thmx",
        ".root",
        ".orig",
        ".text",
        ".jpeg",
        ".bank",
        ".ssce",
        ".sspj",
        ".ssae",
        ".ssee",
        ".cube",
        ".pdf",
        ".png",
        ".txt",
        ".psd",
        ".ini",
        ".wav",
        ".rtf",
        ".fbx",
        ".zip",
        ".mat",
        ".url",
        ".chm",
        ".jpg",
        ".ttf",
        ".mp3",
        ".eps",
        ".mtl",
        ".obj",
        ".odt",
        ".tga",
        ".cdr",
        ".dll",
        ".exr",
        ".xml",
        ".htm",
        ".tif",
        ".wlt",
        ".psb",
        ".mp4",
        ".ogg",
        ".bmp",
        ".dae",
        ".hdr",
        ".uss",
        ".svg",
        ".tss",
        ".otf",
        ".aar",
        ".log",
        ".pdb",
        ".bac",
        ".doc",
        ".exe",
        ".bin",
        ".gif",
        ".vfx",
        ".wmv",
        ".snk",
        ".sln",
        ".cpp",
        ".jar",
        ".dat",
        ".pak",
        ".lib",
        ".raw",
        ".exp",
        ".rpl",
        ".ico",
        ".fla",
        ".pri",
        ".xcf",
        ".fga",
        ".usp",
        ".vox",
        ".rsp",
        ".st9",
        ".car",
        ".tbg",
        ".spm",
        ".nib",
        ".inl",
        ".ply",
        ".pom",
        ".ods",
        ".xls",
        ".abr",
        ".mov",
        ".cs",
        ".md",
        ".ai",
        ".gs",
        ".7z",
        ".db",
        ".mm",
        ".py",
        ".so",
        ".cg",
        ".sh",
        ".bc",
        ".tt",
        ".js",
        ".ht",
        ".0",
        ".a",
        ".h",
        ".m"
    };

    public static string Normalize(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return fileName;

        var lastDotIndex = fileName.LastIndexOf('.');
        if (lastDotIndex <= 0 || lastDotIndex == fileName.Length - 1)
            return fileName;

        var prefix = fileName[..lastDotIndex];
        var suffix = fileName[lastDotIndex..];

        var trimmedSuffix = TrimTrailingZeros(suffix);
        if (!string.Equals(trimmedSuffix, suffix, StringComparison.Ordinal))
            return trimmedSuffix.Length == 0
                ? prefix
                : prefix + trimmedSuffix;

        if (suffix.Length < MinimumCorruptedExtensionLength)
            return fileName;

        foreach (var extension in KnownExtensions.Value)
        {
            if (!suffix.StartsWith(extension, StringComparison.OrdinalIgnoreCase))
                continue;

            if (suffix.Length == extension.Length)
                return fileName;

            return fileName[..(lastDotIndex + extension.Length)];
        }

        return fileName;
    }

    private static string TrimTrailingZeros(string suffix)
    {
        if (!suffix.EndsWith('0'))
            return suffix;

        var index = suffix.Length - 1;
        while (index >= 1 && suffix[index] == '0')
            index--;

        if (index == suffix.Length - 1)
            return suffix;

        if (index < 1)
            return string.Empty;

        return suffix[..(index + 1)];
    }

    private static IReadOnlyList<string> LoadExtensions()
    {
        var overrideFile = TryLocateOverrideFile();
        if (overrideFile is not null)
            try
            {
                return ParseExtensions(File.ReadAllLines(overrideFile));
            }
            catch (IOException)
            {
                // Ignore and fall back to defaults.
            }
            catch (UnauthorizedAccessException)
            {
                // Ignore and fall back to defaults.
            }

        return DefaultExtensions;
    }

    private static string? TryLocateOverrideFile()
    {
        var baseDirectory = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDirectory, "extensions.txt"),
            Path.Combine(baseDirectory, "Assets", "extensions.txt"),
            Path.Combine(baseDirectory, "Assets", "ExtensionNormalization", "extensions.txt")
        };

        foreach (var candidate in candidates)
            if (File.Exists(candidate))
                return candidate;

        return null;
    }

    private static IReadOnlyList<string> ParseExtensions(IEnumerable<string> lines)
    {
        return lines
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .Select(line => line.StartsWith('.') ? line : $".{line}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(line => line.Length)
            .ThenBy(line => line, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}