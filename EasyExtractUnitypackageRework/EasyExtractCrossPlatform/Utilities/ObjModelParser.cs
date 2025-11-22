using System.Numerics;
using System.Text;

namespace EasyExtractCrossPlatform.Utilities;

public static class ObjModelParser
{
    public static ModelPreviewData? TryParse(byte[] data)
    {
        if (data is not { Length: > 0 })
            return null;

        try
        {
            using var stream = new MemoryStream(data);
            using var reader = new StreamReader(stream, Encoding.UTF8, true);
            return ParseInternal(reader);
        }
        catch
        {
            return null;
        }
    }

    private static ModelPreviewData? ParseInternal(TextReader reader)
    {
        var vertices = new List<Vector3>();
        var indices = new List<int>();

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            line = line.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                continue;

            if (line.StartsWith("v ", StringComparison.Ordinal))
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 4)
                    continue;

                if (TryParseFloat(parts[1], out var x) &&
                    TryParseFloat(parts[2], out var y) &&
                    TryParseFloat(parts[3], out var z))
                    vertices.Add(new Vector3(x, y, z));
            }
            else if (line.StartsWith("f ", StringComparison.Ordinal))
            {
                var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length < 4)
                    continue;

                var faceIndices = new List<int>(tokens.Length - 1);
                for (var i = 1; i < tokens.Length; i++)
                {
                    var token = tokens[i];
                    var separatorIndex = token.IndexOf('/');
                    var indexToken = separatorIndex >= 0 ? token[..separatorIndex] : token;

                    if (!int.TryParse(indexToken, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rawIndex))
                        continue;

                    var resolvedIndex = ResolveIndex(rawIndex, vertices.Count);
                    if (resolvedIndex < 0 || resolvedIndex >= vertices.Count)
                        continue;

                    faceIndices.Add(resolvedIndex);
                }

                if (faceIndices.Count < 3)
                    continue;

                for (var i = 1; i < faceIndices.Count - 1; i++)
                {
                    indices.Add(faceIndices[0]);
                    indices.Add(faceIndices[i]);
                    indices.Add(faceIndices[i + 1]);
                }
            }
        }

        if (vertices.Count == 0 || indices.Count == 0)
            return null;

        var center = Vector3.Zero;
        foreach (var vertex in vertices)
            center += vertex;

        center /= vertices.Count;

        var maxDistance = 0f;
        foreach (var vertex in vertices)
        {
            var distance = Vector3.Distance(vertex, center);
            if (distance > maxDistance)
                maxDistance = distance;
        }

        return new ModelPreviewData(vertices.ToArray(), indices.ToArray(), center, Math.Max(maxDistance, 0.0001f));
    }

    private static bool TryParseFloat(string token, out float value)
    {
        return float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static int ResolveIndex(int index, int count)
    {
        if (index > 0)
            return index - 1;

        var resolved = count + index;
        return resolved;
    }
}