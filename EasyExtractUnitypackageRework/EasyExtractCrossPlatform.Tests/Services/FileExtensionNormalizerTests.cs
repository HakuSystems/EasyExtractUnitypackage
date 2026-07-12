using EasyExtract.Core.Utilities;
using Xunit;

namespace EasyExtractCrossPlatform.Tests.Services;

public sealed class FileExtensionNormalizerTests
{
    [Theory]
    [InlineData("Material.029 10", "Material.029 10")]
    [InlineData("Material.029 1", "Material.029 1")]
    [InlineData("Version 2.0", "Version 2.0")]
    [InlineData("GoogleMobileAds-v9.2.0", "GoogleMobileAds-v9.2.0")]
    [InlineData("Track.10", "Track.10")]
    [InlineData("Backup.000", "Backup.000")]
    public void Normalize_KeepsLegitimateNamesEndingInZero(string input, string expected)
    {
        Assert.Equal(expected, FileExtensionNormalizer.Normalize(input));
    }

    [Theory]
    [InlineData("Texture.png0", "Texture.png")]
    [InlineData("Texture.png000", "Texture.png")]
    [InlineData("Model.fbx0", "Model.fbx")]
    public void Normalize_RepairsKnownExtensionsWithTrailingZeros(string input, string expected)
    {
        Assert.Equal(expected, FileExtensionNormalizer.Normalize(input));
    }

    [Fact]
    public void Normalize_TruncatesPathologicallyInflatedKnownExtension()
    {
        var corrupted = "Model.prefab" + new string('x', 40);
        Assert.Equal("Model.prefab", FileExtensionNormalizer.Normalize(corrupted));
    }

    [Theory]
    [InlineData("NoExtension")]
    [InlineData(".hiddenfile")]
    [InlineData("EndsWithDot.")]
    public void Normalize_LeavesNamesWithoutUsableExtensionUntouched(string input)
    {
        Assert.Equal(input, FileExtensionNormalizer.Normalize(input));
    }

    [Fact]
    public void Normalize_KeepsExactKnownExtensions()
    {
        Assert.Equal("Texture.png", FileExtensionNormalizer.Normalize("Texture.png"));
        Assert.Equal("Sound.mp3", FileExtensionNormalizer.Normalize("Sound.mp3"));
    }
}
