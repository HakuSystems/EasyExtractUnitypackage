using EasyExtractCrossPlatform.Utilities;
using Xunit;

namespace EasyExtractCrossPlatform.Tests.Services;

public sealed class PrefabMeshReferenceParserTests
{
    [Fact]
    public void ExtractMeshGuids_FindsMeshFilterAndSkinnedMeshReferences()
    {
        const string yaml = """
            MeshFilter:
              m_GameObject: {fileID: 1904441}
              m_Mesh: {fileID: 4300000, guid: aabbccddeeff00112233445566778899, type: 3}
            SkinnedMeshRenderer:
              m_Mesh: {fileID: -5490181739762837926, guid: 99887766554433221100ffeeddccbbaa, type: 3}
            """;

        var guids = PrefabMeshReferenceParser.ExtractMeshGuids(yaml);

        Assert.Equal(2, guids.Count);
        Assert.Contains("aabbccddeeff00112233445566778899", guids);
        Assert.Contains("99887766554433221100ffeeddccbbaa", guids);
    }

    [Fact]
    public void ExtractMeshGuids_DeduplicatesRepeatedReferences()
    {
        const string yaml = """
            m_Mesh: {fileID: 4300000, guid: aabbccddeeff00112233445566778899, type: 3}
            m_Mesh: {fileID: 4300002, guid: AABBCCDDEEFF00112233445566778899, type: 3}
            """;

        var guids = PrefabMeshReferenceParser.ExtractMeshGuids(yaml);

        Assert.Single(guids);
    }

    [Fact]
    public void ExtractMeshGuids_IgnoresNonMeshGuidsAndNullRefs()
    {
        const string yaml = """
            m_Materials:
            - {fileID: 2100000, guid: 11111111111111111111111111111111, type: 2}
            m_Mesh: {fileID: 0}
            """;

        var guids = PrefabMeshReferenceParser.ExtractMeshGuids(yaml);

        Assert.Empty(guids);
    }

    [Fact]
    public void ExtractMeshGuids_HandlesEmptyInput()
    {
        Assert.Empty(PrefabMeshReferenceParser.ExtractMeshGuids(null));
        Assert.Empty(PrefabMeshReferenceParser.ExtractMeshGuids(string.Empty));
    }
}
