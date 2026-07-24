using EasyExtract.Core.Services;
using EasyExtractCrossPlatform.Services;
using Xunit;

namespace EasyExtractCrossPlatform.Tests.Services;

/// <summary>
///     The container resolves lazily, so a service that is only requested when a window opens would
///     otherwise fail for the first user who clicks the button rather than at startup.
/// </summary>
public sealed class AppServiceProviderTests
{
    [Fact]
    public void DuplicateScanningServicesResolve()
    {
        var provider = AppServiceLocator.Current;

        var assetScanner = provider.GetRequiredService<IDuplicateAssetScanner>();
        var libraryScanner = provider.GetRequiredService<IDuplicateLibraryScanner>();

        Assert.NotNull(assetScanner);
        Assert.NotNull(libraryScanner);
        Assert.Same(assetScanner, provider.GetRequiredService<IDuplicateAssetScanner>());
        Assert.Same(libraryScanner, provider.GetRequiredService<IDuplicateLibraryScanner>());
    }

    [Fact]
    public void CoreServicesResolve()
    {
        var provider = AppServiceLocator.Current;

        Assert.NotNull(provider.GetRequiredService<IUnityPackageExtractionService>());
        Assert.NotNull(provider.GetRequiredService<IUnityPackagePreviewService>());
        Assert.NotNull(provider.GetRequiredService<IMaliciousCodeDetectionService>());
        Assert.NotNull(provider.GetRequiredService<IEverythingSearchService>());
    }
}
