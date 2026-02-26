using System.Globalization;
using System.Text.RegularExpressions;
using EasyExtractCrossPlatform.Services;
using Xunit;

namespace EasyExtractCrossPlatform.Tests.Services;

public sealed class LinuxSearchServiceTests
{
    private static readonly string[] SearchRoots = ["/home", "/mnt"];

    [Fact]
    public void BuildFdArgumentsForQuery_AddsFullPathForForwardSlashQuery()
    {
        var arguments = LinuxSearchService.BuildFdArgumentsForQuery("/home/bruno/Downloads", 50, SearchRoots);

        Assert.Contains("--full-path", arguments);
        Assert.Contains(Regex.Escape("/home/bruno/Downloads"), arguments);
    }

    [Fact]
    public void BuildFdArgumentsForQuery_AddsFullPathForTrailingSlashQuery()
    {
        var arguments = LinuxSearchService.BuildFdArgumentsForQuery("/home/bruno/Downloads/", 50, SearchRoots);

        Assert.Contains("--full-path", arguments);
        Assert.Contains(Regex.Escape("/home/bruno/Downloads/"), arguments);
    }

    [Fact]
    public void BuildFdArgumentsForQuery_AddsFullPathForRootQuery()
    {
        var arguments = LinuxSearchService.BuildFdArgumentsForQuery("/", 50, SearchRoots);

        Assert.Contains("--full-path", arguments);
        Assert.Contains("/", arguments);
    }

    [Fact]
    public void BuildFdArgumentsForQuery_DoesNotAddFullPathForNameQuery()
    {
        const string query = "animals pack";
        var arguments = LinuxSearchService.BuildFdArgumentsForQuery(query, 50, SearchRoots);

        Assert.DoesNotContain("--full-path", arguments);
        Assert.Contains(Regex.Escape(query), arguments);
    }

    [Fact]
    public void BuildFdArgumentsForQuery_NormalizesBackslashQueryAndAddsFullPath()
    {
        var arguments = LinuxSearchService.BuildFdArgumentsForQuery(@"home\bruno\Downloads", 50, SearchRoots);

        Assert.Contains("--full-path", arguments);
        Assert.Contains(Regex.Escape("home/bruno/Downloads"), arguments);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(99999, 2000)]
    public void BuildFdArgumentsForQuery_ClampsMaxResults(int requested, int expected)
    {
        var arguments = LinuxSearchService.BuildFdArgumentsForQuery("animals", requested, SearchRoots);
        var maxResultsIndex = arguments.ToList().IndexOf("--max-results");

        Assert.NotEqual(-1, maxResultsIndex);
        Assert.True(maxResultsIndex + 1 < arguments.Count);
        Assert.Equal(expected.ToString(CultureInfo.InvariantCulture), arguments[maxResultsIndex + 1]);
    }
}