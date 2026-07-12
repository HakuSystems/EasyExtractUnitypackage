using System.Numerics;
using Assimp;

namespace EasyExtractCrossPlatform.Utilities;

public static class ModelPreviewParser
{
    private const PostProcessSteps DefaultPostProcessSteps =
        PostProcessSteps.Triangulate |
        PostProcessSteps.JoinIdenticalVertices |
        PostProcessSteps.ImproveCacheLocality |
        PostProcessSteps.SortByPrimitiveType |
        PostProcessSteps.PreTransformVertices;

    public static ModelPreviewData? TryParse(byte[] data, string extension)
    {
        return TryParse(data, null, extension);
    }

    public static ModelPreviewData? TryParse(byte[]? data, string? filePath, string extension)
    {
        if (data is { Length: > 0 })
        {
            if (string.Equals(extension, ".obj", StringComparison.OrdinalIgnoreCase))
                return ObjModelParser.TryParse(data);

            return TryParseWithAssimp(context =>
            {
                using var stream = new MemoryStream(data, false);
                return context.ImportFileFromStream(stream, DefaultPostProcessSteps, NormalizeFormatHint(extension));
            });
        }

        if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
            return TryParseWithAssimp(context => context.ImportFile(filePath, DefaultPostProcessSteps));

        return null;
    }

    public static ModelPreviewData? Combine(IReadOnlyList<ModelPreviewData> models)
    {
        if (models.Count == 0)
            return null;

        if (models.Count == 1)
            return models[0];

        var vertices = new List<Vector3>();
        var indices = new List<int>();

        foreach (var model in models)
        {
            var baseIndex = vertices.Count;
            vertices.AddRange(model.Vertices);
            foreach (var index in model.Indices)
                indices.Add(baseIndex + index);
        }

        if (vertices.Count == 0 || indices.Count == 0)
            return null;

        var center = ComputeCenter(vertices);
        return new ModelPreviewData(vertices.ToArray(), indices.ToArray(), center,
            ComputeBoundingRadius(vertices, center));
    }

    private static ModelPreviewData? TryParseWithAssimp(Func<AssimpContext, Scene?> import)
    {
        try
        {
            using var context = new AssimpContext();
            var scene = import(context);
            if (scene is null || !scene.HasMeshes)
                return null;

            var vertices = new List<Vector3>();
            var indices = new List<int>();

            foreach (var mesh in scene.Meshes)
            {
                if (!mesh.HasVertices)
                    continue;

                var baseIndex = vertices.Count;
                for (var i = 0; i < mesh.VertexCount; i++)
                {
                    var vertex = mesh.Vertices[i];
                    vertices.Add(new Vector3(vertex.X, vertex.Y, vertex.Z));
                }

                foreach (var face in mesh.Faces)
                {
                    if (face.IndexCount < 3)
                        continue;

                    var faceIndices = face.Indices;
                    for (var i = 1; i < faceIndices.Count - 1; i++)
                    {
                        indices.Add(baseIndex + faceIndices[0]);
                        indices.Add(baseIndex + faceIndices[i]);
                        indices.Add(baseIndex + faceIndices[i + 1]);
                    }
                }
            }

            if (vertices.Count == 0 || indices.Count == 0)
                return null;

            var center = ComputeCenter(vertices);
            var boundingRadius = ComputeBoundingRadius(vertices, center);

            return new ModelPreviewData(vertices.ToArray(), indices.ToArray(), center, boundingRadius);
        }
        catch (AssimpException)
        {
            return null;
        }
        catch
        {
            return null;
        }
    }

    private static string? NormalizeFormatHint(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return null;

        return extension.StartsWith('.') ? extension[1..] : extension;
    }

    private static Vector3 ComputeCenter(IReadOnlyList<Vector3> vertices)
    {
        if (vertices.Count == 0)
            return Vector3.Zero;

        var sum = Vector3.Zero;
        for (var i = 0; i < vertices.Count; i++)
            sum += vertices[i];

        return sum / vertices.Count;
    }

    private static float ComputeBoundingRadius(IReadOnlyList<Vector3> vertices, Vector3 center)
    {
        var radius = 0f;
        for (var i = 0; i < vertices.Count; i++)
        {
            var distance = Vector3.Distance(vertices[i], center);
            if (distance > radius)
                radius = distance;
        }

        return Math.Max(radius, 0.0001f);
    }
}