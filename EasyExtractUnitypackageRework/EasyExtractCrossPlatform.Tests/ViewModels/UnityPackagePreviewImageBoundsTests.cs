using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using EasyExtract.Core.Models;
using EasyExtract.Core.Services;
using EasyExtractCrossPlatform.Services;
using EasyExtractCrossPlatform.Tests.Support;
using EasyExtractCrossPlatform.ViewModels;
using Xunit;

namespace EasyExtractCrossPlatform.Tests.ViewModels;

/// <summary>
///     Asset previews decode images that came out of a downloaded package, so their size is
///     attacker controlled. Decoding one at full resolution is what killed V2.9.6 with
///     "Unable to allocate pixels for the bitmap"; that was fixed for the background wallpaper
///     but the preview path kept decoding unbounded.
/// </summary>
public sealed class UnityPackagePreviewImageBoundsTests
{
    [Fact]
    public Task PrimaryImagePreview_IsCappedForOversizedAssets()
    {
        return HeadlessUi.RunAsync(() =>
        {
            var viewModel = CreateViewModel();
            var oversized = CreatePng(BoundedImageDecoder.MaxDecodeWidth + 1500, 4);

            viewModel.SelectedAsset = CreateAssetItem("Assets/huge.png", assetData: oversized);

            Assert.NotNull(viewModel.ImagePreview);
            Assert.Equal(BoundedImageDecoder.MaxDecodeWidth, viewModel.ImagePreview!.PixelSize.Width);
        });
    }

    [Fact]
    public Task PrimaryImagePreview_KeepsNormalAssetsAtTheirOwnSize()
    {
        return HeadlessUi.RunAsync(() =>
        {
            var viewModel = CreateViewModel();
            var normal = CreatePng(512, 256);

            viewModel.SelectedAsset = CreateAssetItem("Assets/normal.png", assetData: normal);

            Assert.NotNull(viewModel.ImagePreview);
            Assert.Equal(512, viewModel.ImagePreview!.PixelSize.Width);
            Assert.Equal(256, viewModel.ImagePreview.PixelSize.Height);
        });
    }

    private static UnityPackagePreviewViewModel CreateViewModel()
    {
        return new UnityPackagePreviewViewModel(new StubPreviewService(), "/packages/test.unitypackage");
    }

    private static UnityPackageAssetPreviewItem CreateAssetItem(string relativePath, byte[] assetData)
    {
        return new UnityPackageAssetPreviewItem(new UnityPackagePreviewAsset(
            relativePath,
            assetData.Length,
            false,
            null,
            assetData,
            false,
            null));
    }

    private static byte[] CreatePng(int width, int height)
    {
        var bitmap = new WriteableBitmap(
            new PixelSize(width, height),
            new Vector(96, 96),
            PixelFormat.Rgba8888,
            AlphaFormat.Unpremul);

        using var stream = new MemoryStream();
        bitmap.Save(stream);
        return stream.ToArray();
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
