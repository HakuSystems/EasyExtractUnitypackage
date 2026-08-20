using EasyExtract.Core.Models;
using EasyExtractCrossPlatform;
using Xunit;

namespace EasyExtractCrossPlatform.Tests;

/// <summary>
///     "Packages completed" used to grow on every run that did not throw, so a package we failed to
///     parse still moved the counter. These tests pin down which runs the statistics may count.
/// </summary>
public sealed class MainWindowStatisticsTests
{
    [Fact]
    public void CountsAsCompletedPackage_WithWrittenAssets_IsTrue()
    {
        var result = Result(3, UnityPackageExtractionOutcome.Extracted);

        Assert.True(MainWindow.CountsAsCompletedPackage(result));
    }

    [Fact]
    public void CountsAsCompletedPackage_WhenNothingWasRecognized_IsFalse()
    {
        var result = Result(0, UnityPackageExtractionOutcome.NoAssetsRecognized);

        Assert.False(MainWindow.CountsAsCompletedPackage(result));
    }

    [Fact]
    public void CountsAsCompletedPackage_WhenThereWasNothingToDo_IsFalse()
    {
        var result = Result(0, UnityPackageExtractionOutcome.NothingToDo);

        Assert.False(MainWindow.CountsAsCompletedPackage(result));
    }

    /// <summary>
    ///     Outcome and count contradicting each other would be a service bug. The counter must not
    ///     grow on it either way, so the predicate checks both instead of trusting one.
    /// </summary>
    [Fact]
    public void CountsAsCompletedPackage_WithExtractedButZeroAssets_IsFalse()
    {
        var result = Result(0, UnityPackageExtractionOutcome.Extracted);

        Assert.False(MainWindow.CountsAsCompletedPackage(result));
    }

    [Fact]
    public void CountsAsCompletedPackage_WithNull_IsFalse()
    {
        Assert.False(MainWindow.CountsAsCompletedPackage(null));
    }

    private static UnityPackageExtractionResult Result(int assetsExtracted, UnityPackageExtractionOutcome outcome)
    {
        return new UnityPackageExtractionResult(
            "C:/packages/example.unitypackage",
            "C:/output",
            assetsExtracted,
            Array.Empty<string>())
        {
            Outcome = outcome
        };
    }
}
