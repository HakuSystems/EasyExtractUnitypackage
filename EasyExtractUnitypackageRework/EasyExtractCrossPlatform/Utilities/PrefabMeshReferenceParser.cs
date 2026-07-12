using System.Text.RegularExpressions;

namespace EasyExtractCrossPlatform.Utilities;

/// <summary>
///     Extracts the mesh asset GUIDs a Unity prefab references, so the preview
///     can render the actual model files shipped in the same package.
/// </summary>
public static partial class PrefabMeshReferenceParser
{
    private const int MaxReferences = 8;

    [GeneratedRegex(@"m_Mesh:\s*\{fileID:\s*-?\d+,\s*guid:\s*([0-9a-fA-F]{32})",
        RegexOptions.CultureInvariant)]
    private static partial Regex MeshReferenceRegex();

    public static IReadOnlyList<string> ExtractMeshGuids(string? prefabYaml)
    {
        if (string.IsNullOrWhiteSpace(prefabYaml))
            return Array.Empty<string>();

        var guids = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in MeshReferenceRegex().Matches(prefabYaml))
        {
            var guid = match.Groups[1].Value;
            if (!seen.Add(guid))
                continue;

            guids.Add(guid);
            if (guids.Count >= MaxReferences)
                break;
        }

        return guids;
    }
}
