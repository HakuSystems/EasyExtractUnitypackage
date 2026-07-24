using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using EasyExtractCrossPlatform.Services;
using EasyExtractCrossPlatform.Tests.Support;
using Xunit;

namespace EasyExtractCrossPlatform.Tests.Services;

/// <summary>
///     Decoding an image at its full size is what made V2.9.6 die with "Unable to allocate pixels
///     for the bitmap". That was fixed for the background wallpaper only, while asset previews kept
///     decoding unbounded, and those come out of downloaded packages rather than the user's own
///     picture folder. These tests exercise the real decode instead of just the width helper.
/// </summary>
public sealed class BoundedImageDecoderDecodeTests
{
    [Fact]
    public Task Decode_CapsImagesWiderThanTheBound()
    {
        return HeadlessUi.RunAsync(() =>
        {
            var png = CreatePng(BoundedImageDecoder.MaxDecodeWidth + 2000, 4);

            using var stream = new MemoryStream(png);
            using var bitmap = BoundedImageDecoder.Decode(stream);

            Assert.Equal(BoundedImageDecoder.MaxDecodeWidth, bitmap.PixelSize.Width);
        });
    }

    [Fact]
    public Task Decode_LeavesSmallerImagesUntouched()
    {
        return HeadlessUi.RunAsync(() =>
        {
            var png = CreatePng(640, 480);

            using var stream = new MemoryStream(png);
            using var bitmap = BoundedImageDecoder.Decode(stream);

            Assert.Equal(640, bitmap.PixelSize.Width);
            Assert.Equal(480, bitmap.PixelSize.Height);
        });
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
}
