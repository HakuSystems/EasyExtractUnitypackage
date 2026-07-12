using System.Text;
using EasyExtractCrossPlatform.Utilities;
using Xunit;

namespace EasyExtractCrossPlatform.Tests.Services;

public sealed class ModelPreviewParserTests
{
    private const string AsciiStlTriangle = """
        solid triangle
          facet normal 0 0 1
            outer loop
              vertex 0 0 0
              vertex 1 0 0
              vertex 0 1 0
            endloop
          endfacet
        endsolid triangle
        """;

    [Fact]
    public void TryParse_ParsesStlFromMemory()
    {
        var data = Encoding.ASCII.GetBytes(AsciiStlTriangle);

        var model = ModelPreviewParser.TryParse(data, null, ".stl");

        Assert.NotNull(model);
        Assert.Equal(3, model!.Vertices.Length);
        Assert.Equal(3, model.Indices.Length);
        Assert.True(model.BoundingRadius > 0);
    }

    [Fact]
    public void TryParse_ParsesStlFromFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"easyextract-test-{Guid.NewGuid():N}.stl");
        File.WriteAllText(path, AsciiStlTriangle, Encoding.ASCII);

        try
        {
            var model = ModelPreviewParser.TryParse(null, path, ".stl");

            Assert.NotNull(model);
            Assert.Equal(3, model!.Vertices.Length);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void TryParse_ReturnsNullForGarbageData()
    {
        var model = ModelPreviewParser.TryParse(Encoding.ASCII.GetBytes("not a mesh"), null, ".fbx");

        Assert.Null(model);
    }

    [Fact]
    public void TryParse_ReturnsNullWithoutDataOrFile()
    {
        Assert.Null(ModelPreviewParser.TryParse(null, null, ".fbx"));
        Assert.Null(ModelPreviewParser.TryParse(null, "Z:\\does\\not\\exist.fbx", ".fbx"));
    }

    [Fact]
    public void Combine_MergesVerticesAndReindexesFaces()
    {
        var data = Encoding.ASCII.GetBytes(AsciiStlTriangle);
        var first = ModelPreviewParser.TryParse(data, null, ".stl");
        var second = ModelPreviewParser.TryParse(data, null, ".stl");
        Assert.NotNull(first);
        Assert.NotNull(second);

        var combined = ModelPreviewParser.Combine(new[]
        {
            first!, second!
        });

        Assert.NotNull(combined);
        Assert.Equal(6, combined!.Vertices.Length);
        Assert.Equal(6, combined.Indices.Length);
        Assert.Equal(3, combined.Indices.Skip(3).Min());
    }
}
