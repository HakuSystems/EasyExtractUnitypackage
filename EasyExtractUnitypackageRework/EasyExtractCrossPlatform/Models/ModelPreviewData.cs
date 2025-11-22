using System.Numerics;

namespace EasyExtractCrossPlatform.Models;

public sealed class ModelPreviewData
{
    public ModelPreviewData(Vector3[] vertices, int[] indices, Vector3 center, float boundingRadius)
    {
        Vertices = vertices ?? throw new ArgumentNullException(nameof(vertices));
        Indices = indices ?? throw new ArgumentNullException(nameof(indices));
        Center = center;
        BoundingRadius = boundingRadius;
    }

    public Vector3[] Vertices { get; }

    /// <summary>
    ///     Triangulated indices (every consecutive triplet forms a triangle).
    /// </summary>
    public int[] Indices { get; }

    public Vector3 Center { get; }

    public float BoundingRadius { get; }
}