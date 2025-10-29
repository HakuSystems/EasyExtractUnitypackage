using Avalonia.Media;

namespace EasyExtractCrossPlatform.ViewModels;

internal static class UnityAssetIconProvider
{
    public static readonly Geometry ChevronRight = Parse("M3,5 L11,13 L3,21 Z");
    public static readonly Geometry ChevronDown = Parse("M5,9 L13,17 L21,9 Z");

    public static readonly Geometry CollapseAll =
        Parse("M5,17 L13,9 L21,17 L18,17 L13,12 L8,17 Z M5,11 L13,3 L21,11 L18,11 L13,6 L8,11 Z");

    public static readonly Geometry FolderClosed = Parse("M3,10 H10 L12,12 H21 V22 H3 Z");
    public static readonly Geometry FolderOpen = Parse("M3,14 H21 L19,22 H5 Z");
    public static readonly Geometry File = Parse("M6,4 H16 L21,9 V24 H6 Z");
    public static readonly Geometry Script = Parse("M6,4 H16 L21,9 V24 H6 Z M9,11 H11 V15 H9 Z M15,11 H17 V15 H15 Z");
    public static readonly Geometry Texture = Parse("M4,6 H20 V22 H4 Z M7,11 L11.5,15.5 L14.5,12.5 L18,18 H7 Z");

    public static readonly Geometry Audio =
        Parse(
            "M8,10 H12 L16,6 V18 C16,20.209 14.209,22 12,22 C9.791,22 8,20.209 8,18 C8,15.791 9.791,14 12,14 C12.334,14 12.66,14.041 12.97,14.118 V10 H8 Z");

    public static readonly Geometry Model = Parse("M6,8 L12,4 L18,8 V16 L12,20 L6,16 Z M12,4 V12 L6,16 M12,12 L18,16");

    public static readonly Geometry Material =
        Parse("M12,4 L19,8 L19,16 L12,20 L5,16 L5,8 Z M12,10 L15,12 L12,14 L9,12 Z");

    public static readonly Geometry Prefab =
        Parse("M12,4 L14.5,9.5 L20,10 L16,14 L17.5,20 L12,17 L6.5,20 L8,14 L4,10 L9.5,9.5 Z");

    public static readonly Geometry Animation = Parse("M4,6 H20 V22 H4 Z M7,6 V22 M13,6 V22 M4,11 H20 M4,17 H20");

    public static readonly Geometry Shader =
        Parse("M12,4 L13.5,8.5 L18.5,9 L14.5,12.5 L15.8,17.5 L12,15 L8.2,17.5 L9.5,12.5 L5.5,9 L10.5,8.5 Z");

    public static Geometry GetChevron(bool isExpanded)
    {
        return isExpanded ? ChevronDown : ChevronRight;
    }

    public static Geometry GetFolderIcon(bool isExpanded)
    {
        return isExpanded ? FolderOpen : FolderClosed;
    }

    public static Geometry GetAssetIcon(UnityPackageAssetPreviewItem? asset)
    {
        return GetAssetIcon(asset?.Category);
    }

    public static Geometry GetAssetIcon(string? category)
    {
        return category switch
        {
            "Script" => Script,
            "Texture" => Texture,
            "3D Model" => Model,
            "Audio" => Audio,
            "DLL" => File,
            "Animation" => Animation,
            "Shader" => Shader,
            "Material" => Material,
            "Prefab" => Prefab,
            "Folder" => FolderClosed,
            _ => File
        };
    }

    private static Geometry Parse(string data)
    {
        return StreamGeometry.Parse(data);
    }
}