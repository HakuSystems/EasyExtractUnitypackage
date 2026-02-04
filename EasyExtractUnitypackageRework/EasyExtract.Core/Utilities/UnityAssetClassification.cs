namespace EasyExtract.Core.Utilities;

public static class UnityAssetClassification
{
    private static readonly Dictionary<string, string> ExtensionToCategory = new(StringComparer.OrdinalIgnoreCase)
    {
        // Textures
        { ".png", "Texture" }, { ".jpg", "Texture" }, { ".jpeg", "Texture" }, { ".tga", "Texture" },
        { ".tif", "Texture" }, { ".tiff", "Texture" }, { ".psd", "Texture" }, { ".bmp", "Texture" },
        { ".dds", "Texture" }, { ".gif", "Texture" }, { ".hdr", "Texture" }, { ".exr", "Texture" },

        // 3D Models
        { ".fbx", "3D Model" }, { ".obj", "3D Model" }, { ".dae", "3D Model" }, { ".blend", "3D Model" },
        { ".3ds", "3D Model" }, { ".dxf", "3D Model" }, { ".stl", "3D Model" },

        // Audio
        { ".wav", "Audio" }, { ".wave", "Audio" }, { ".mp3", "Audio" }, { ".ogg", "Audio" },
        { ".oga", "Audio" }, { ".aiff", "Audio" }, { ".aif", "Audio" }, { ".flac", "Audio" },
        { ".m4a", "Audio" }, { ".aac", "Audio" }, { ".wma", "Audio" }, { ".opus", "Audio" },
        { ".caf", "Audio" }, { ".au", "Audio" },

        // Scripts & Code
        { ".cs", "Script" }, { ".js", "Script" }, { ".boo", "Script" }, { ".asmdef", "Script" },

        // Shaders
        { ".shader", "Shader" }, { ".cg", "Shader" }, { ".cginc", "Shader" }, { ".compute", "Shader" },
        { ".shadergraph", "Shader" }, { ".shadersubgraph", "Shader" }, { ".hlsl", "Shader" },
        { ".glsl", "Shader" },

        // Plugins
        { ".dll", "DLL" },

        // Animations
        { ".anim", "Animation" }, { ".controller", "Animation" },
        { ".overridecontroller", "Animation" }, { ".mask", "Animation" },

        // Materials
        { ".mat", "Material" }, { ".physicmaterial", "Material" },

        // Prefabs
        { ".prefab", "Prefab" },

        // Scenes
        { ".unity", "Scene" },

        // Fonts
        { ".ttf", "Font" }, { ".otf", "Font" },

        // Documents / Configs
        { ".pdf", "Document" }, { ".txt", "Document" }, { ".md", "Document" }, { ".rtf", "Document" },
        { ".json", "Document" }, { ".xml", "Document" }, { ".yml", "Document" }, { ".yaml", "Document" },
        { ".uss", "Document" }, { ".uxml", "Document" },

        // Videos
        { ".mp4", "Video" }, { ".mov", "Video" }, { ".webm", "Video" },

        // Generic Asset
        { ".asset", "Asset" }
    };

    public static string ResolveCategory(string? relativePath, long assetSizeBytes, bool hasAssetData)
    {
        var extension = string.IsNullOrWhiteSpace(relativePath)
            ? string.Empty
            : Path.GetExtension(relativePath)?.ToLowerInvariant() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(extension))
        {
            if (IsLikelyFolder(relativePath, assetSizeBytes, hasAssetData))
                return "Folder";
            return "Other";
        }

        if (ExtensionToCategory.TryGetValue(extension, out var category)) return category;

        // Fallback: It has an extension (or looks like it), but it's not in our list.
        // It might be a folder named "My.Folder" or similar.
        if (IsLikelyFolder(relativePath, assetSizeBytes, hasAssetData)) return "Folder";

        return "Other";
    }

    // --- Backward Compatibility Helpers ---

    public static bool IsTextureExtension(string? extension)
    {
        return !string.IsNullOrWhiteSpace(extension) && ExtensionToCategory.TryGetValue(extension, out var cat) &&
               cat == "Texture";
    }

    public static bool IsPdfExtension(string? extension)
    {
        // PDF is now categorized as "Document", but specific check might be needed for PDF preview logic.
        return !string.IsNullOrWhiteSpace(extension) &&
               string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsModelExtension(string? extension)
    {
        return !string.IsNullOrWhiteSpace(extension) && ExtensionToCategory.TryGetValue(extension, out var cat) &&
               cat == "3D Model";
    }

    public static bool IsAudioExtension(string? extension)
    {
        return !string.IsNullOrWhiteSpace(extension) && ExtensionToCategory.TryGetValue(extension, out var cat) &&
               cat == "Audio";
    }

    public static bool IsScriptExtension(string? extension)
    {
        return !string.IsNullOrWhiteSpace(extension) && ExtensionToCategory.TryGetValue(extension, out var cat) &&
               cat == "Script";
    }

    public static bool IsAnimationExtension(string? extension)
    {
        return !string.IsNullOrWhiteSpace(extension) && ExtensionToCategory.TryGetValue(extension, out var cat) &&
               cat == "Animation";
    }

    public static bool IsShaderExtension(string? extension)
    {
        return !string.IsNullOrWhiteSpace(extension) && ExtensionToCategory.TryGetValue(extension, out var cat) &&
               cat == "Shader";
    }

    public static bool IsMaterialExtension(string? extension)
    {
        return !string.IsNullOrWhiteSpace(extension) && ExtensionToCategory.TryGetValue(extension, out var cat) &&
               cat == "Material";
    }

    // Kept internal/private helpers if needed, but since they weren't public in original mostly, we just ensure public ones are safe.

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