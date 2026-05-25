using Xunit;

namespace EasyExtractCrossPlatform.Tests;

public sealed class MainWindowDropHelpersTests
{
    [Theory]
    [InlineData(" \"C:\\Packages\\My Pack.unitypackage\" ", "C:\\Packages\\My Pack.unitypackage")]
    [InlineData("'file:///home/user/My%20Pack.unitypackage'", "file:///home/user/My%20Pack.unitypackage")]
    [InlineData("“/Users/me/My Pack.unitypackage”", "/Users/me/My Pack.unitypackage")]
    public void NormalizeDroppedTextEntry_RemovesCommonSurroundingQuotes(string input, string expected)
    {
        var actual = MainWindow.NormalizeDroppedTextEntry(input);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("\"C:\\Packages\\My Pack.unitypackage\"")]
    [InlineData("'file:///home/user/My%20Pack.unitypackage'")]
    [InlineData("“/Users/me/My Pack.unitypackage”")]
    public void IsUnityPackage_AcceptsQuotedPackageCandidates(string input)
    {
        Assert.True(MainWindow.IsUnityPackage(input));
    }

    [Fact]
    public void TryResolveDroppedPath_AcceptsQuotedAbsoluteFilePath()
    {
        var root = Path.Combine(Path.GetTempPath(), "EasyExtractTests", nameof(MainWindowDropHelpersTests),
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var packagePath = Path.Combine(root, "My Package.unitypackage");
            File.WriteAllText(packagePath, string.Empty);

            var result = MainWindow.TryResolveDroppedPath($"\"{packagePath}\"", out var resolvedPath);

            Assert.True(result);
            Assert.Equal(Path.GetFullPath(packagePath), resolvedPath);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [Fact]
    public void GetDroppedPathComparer_MatchesCurrentPlatformPathCaseSensitivity()
    {
        var comparer = MainWindow.GetDroppedPathComparer();

        if (OperatingSystem.IsWindows())
            Assert.True(comparer.Equals("Package.unitypackage", "package.unitypackage"));
        else
            Assert.False(comparer.Equals("Package.unitypackage", "package.unitypackage"));
    }
}