namespace EasyExtractCrossPlatform.Utilities;

internal static class UnityAssetClassification
{
    private static readonly HashSet<string> TextureExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".tga", ".tif", ".tiff", ".psd", ".bmp", ".dds", ".gif", ".hdr", ".exr"
    };

    private static readonly HashSet<string> PdfExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf"
    };

    private static readonly HashSet<string> ModelExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".fbx", ".obj", ".dae", ".blend", ".3ds"
    };

    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".wav", ".wave", ".mp3", ".ogg", ".oga", ".aiff", ".aif", ".flac", ".m4a", ".aac", ".wma", ".opus", ".caf",
        ".au"
    };

    private static readonly HashSet<string> PluginExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".dll"
    };

    private static readonly HashSet<string> ScriptExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".js", ".boo"
    };

    private static readonly HashSet<string> AnimationExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".anim", ".controller", ".overridecontroller", ".mask"
    };

    private static readonly HashSet<string> ShaderExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".shader", ".cg", ".cginc", ".compute", ".shadergraph", ".shadersubgraph", ".hlsl", ".glsl"
    };

    private static readonly HashSet<string> MaterialExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mat"
    };

    internal static string ResolveCategory(string? relativePath, long assetSizeBytes, bool hasAssetData)
    {
        var extension = string.IsNullOrWhiteSpace(relativePath)
            ? string.Empty
            : Path.GetExtension(relativePath)?.ToLowerInvariant() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(extension))
        {
            if (IsLikelyFolder(relativePath, assetSizeBytes, hasAssetData))
                return "Folder";
        }
        else
        {
            if (IsTextureExtension(extension) || IsPdfExtension(extension))
                return "Texture";

            if (IsModelExtension(extension))
                return "3D Model";

            if (IsAudioExtension(extension))
                return "Audio";

            if (IsPluginExtension(extension))
                return "DLL";

            if (IsScriptExtension(extension))
                return "Script";

            if (IsAnimationExtension(extension))
                return "Animation";

            if (IsShaderExtension(extension))
                return "Shader";

            if (IsMaterialExtension(extension))
                return "Material";

            if (extension.Equals(".prefab", StringComparison.OrdinalIgnoreCase))
                return "Prefab";
        }

        return "Other";
    }

    internal static bool IsTextureExtension(string? extension)
    {
        return !string.IsNullOrWhiteSpace(extension) && TextureExtensions.Contains(extension);
    }

    internal static bool IsPdfExtension(string? extension)
    {
        return !string.IsNullOrWhiteSpace(extension) && PdfExtensions.Contains(extension);
    }

    internal static bool IsModelExtension(string? extension)
    {
        return !string.IsNullOrWhiteSpace(extension) && ModelExtensions.Contains(extension);
    }

    internal static bool IsAudioExtension(string? extension)
    {
        return !string.IsNullOrWhiteSpace(extension) && AudioExtensions.Contains(extension);
    }

    internal static bool IsScriptExtension(string? extension)
    {
        return !string.IsNullOrWhiteSpace(extension) && ScriptExtensions.Contains(extension);
    }

    internal static bool IsAnimationExtension(string? extension)
    {
        return !string.IsNullOrWhiteSpace(extension) && AnimationExtensions.Contains(extension);
    }

    internal static bool IsShaderExtension(string? extension)
    {
        return !string.IsNullOrWhiteSpace(extension) && ShaderExtensions.Contains(extension);
    }

    internal static bool IsMaterialExtension(string? extension)
    {
        return !string.IsNullOrWhiteSpace(extension) && MaterialExtensions.Contains(extension);
    }

    private static bool IsPluginExtension(string? extension)
    {
        return !string.IsNullOrWhiteSpace(extension) && PluginExtensions.Contains(extension);
    }

    private static bool IsLikelyFolder(string? relativePath, long assetSizeBytes, bool hasAssetData)
    {
        if (hasAssetData || assetSizeBytes > 0)
            return false;

        if (string.IsNullOrWhiteSpace(relativePath))
            return true;

        var name = Path.GetFileName(relativePath);
        if (string.IsNullOrWhiteSpace(name))
            return true;

        return !name.Contains('.', StringComparison.Ordinal);
    }
}