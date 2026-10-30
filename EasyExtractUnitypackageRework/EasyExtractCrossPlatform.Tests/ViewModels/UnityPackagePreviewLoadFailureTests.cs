using EasyExtract.Core.Models;
using EasyExtract.Core.Services;
using EasyExtractCrossPlatform.Tests.Support;
using EasyExtractCrossPlatform.ViewModels;
using Xunit;

namespace EasyExtractCrossPlatform.Tests.ViewModels;

public sealed class UnityPackagePreviewLoadFailureTests
{
    [Fact]
    public void BuildLoadFailureMessage_ReplacesTheRuntimeTextForOutOfMemory()
    {
        var message = UnityPackagePreviewViewModel.BuildLoadFailureMessage(new OutOfMemoryException());

        Assert.Contains("memory", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("System.OutOfMemoryException", message);
    }

    [Fact]
    public void BuildLoadFailureMessage_KeepsTheMessageForOtherFailures()
    {
        var message = UnityPackagePreviewViewModel.BuildLoadFailureMessage(new IOException("disk gone"));

        Assert.Equal("Failed to load preview: disk gone", message);
    }

    [Fact]
    public Task CanExportSelectedAsset_IsOffForAssetsWithoutLoadedData()
    {
        return HeadlessUi.RunAsync(() =>
        {
            var viewModel = new UnityPackagePreviewViewModel(new StubPreviewService(), "/packages/test.unitypackage");

            viewModel.SelectedAsset = CreateAssetItem([1, 2, 3], false);
            Assert.True(viewModel.CanExportSelectedAsset);

            viewModel.SelectedAsset = CreateAssetItem(null, true);
            Assert.False(viewModel.CanExportSelectedAsset);
        });
    }

    private static UnityPackageAssetPreviewItem CreateAssetItem(byte[]? assetData, bool truncated)
    {
        return new UnityPackageAssetPreviewItem(new UnityPackagePreviewAsset(
            "Assets/item.bin",
            assetData?.Length ?? 0,
            false,
            null,
            assetData,
            truncated,
            null));
    }

    private sealed class StubPreviewService : IUnityPackagePreviewService
    {
        public Task<UnityPackagePreviewResult> LoadPreviewAsync(string packagePath,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("The preview is driven directly in this test.");
        }
    }
}
