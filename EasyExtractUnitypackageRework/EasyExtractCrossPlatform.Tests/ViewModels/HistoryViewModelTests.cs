using EasyExtractCrossPlatform.Models;
using EasyExtractCrossPlatform.ViewModels;
using Xunit;

namespace EasyExtractCrossPlatform.Tests.ViewModels;

public sealed class HistoryViewModelTests
{
    private const double VelocityGraphHeight = 100d;
    private const double MinimumBarHeight = 8d;

    /// <summary>
    ///     The velocity bar heights are a share of the tallest bar. AssetsExtracted is an int, so
    ///     dividing it by the int peak truncated every non peak bar to zero and the whole chart
    ///     collapsed to the minimum height.
    /// </summary>
    [Fact]
    public void VelocityPoints_ScaleProportionallyToThePeak()
    {
        var viewModel = new HistoryViewModel(
        [
            CreateExtractedEntry(assetsExtracted: 100, minutesAgo: 30),
            CreateExtractedEntry(assetsExtracted: 50, minutesAgo: 20),
            CreateExtractedEntry(assetsExtracted: 25, minutesAgo: 10)
        ]);

        Assert.Equal(3, viewModel.VelocityPoints.Count);
        Assert.Equal(VelocityGraphHeight, viewModel.VelocityPoints[0].Height, 3);
        Assert.Equal(VelocityGraphHeight / 2, viewModel.VelocityPoints[1].Height, 3);
        Assert.Equal(VelocityGraphHeight / 4, viewModel.VelocityPoints[2].Height, 3);
    }

    [Fact]
    public void VelocityPoints_KeepTinyValuesAboveTheMinimumBarHeight()
    {
        var viewModel = new HistoryViewModel(
        [
            CreateExtractedEntry(assetsExtracted: 10_000, minutesAgo: 20),
            CreateExtractedEntry(assetsExtracted: 1, minutesAgo: 10)
        ]);

        Assert.Equal(MinimumBarHeight, viewModel.VelocityPoints[1].Height, 3);
    }

    [Fact]
    public void VelocityPoints_AreEmptyWithoutExtractions()
    {
        var viewModel = new HistoryViewModel([CreateQueuedEntry()]);

        Assert.Empty(viewModel.VelocityPoints);
        Assert.False(viewModel.HasVelocityPoints);
    }

    private static HistoryEntry CreateExtractedEntry(int assetsExtracted, int minutesAgo)
    {
        var timestamp = DateTimeOffset.UtcNow.AddMinutes(-minutesAgo);

        return new HistoryEntry
        {
            FileName = $"package-{assetsExtracted}.unitypackage",
            FilePath = $"/packages/package-{assetsExtracted}.unitypackage",
            AddedUtc = timestamp,
            LastSeenUtc = timestamp,
            ExtractedUtc = timestamp,
            WasExtracted = true,
            AssetsExtracted = assetsExtracted
        };
    }

    private static HistoryEntry CreateQueuedEntry()
    {
        return new HistoryEntry
        {
            FileName = "queued.unitypackage",
            FilePath = "/packages/queued.unitypackage"
        };
    }
}
