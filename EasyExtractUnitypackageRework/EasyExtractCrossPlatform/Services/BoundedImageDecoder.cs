using StbImageSharp;

namespace EasyExtractCrossPlatform.Services;

/// <summary>
///     Decodes images with an upper bound on the decoded width, so a single huge image cannot
///     exhaust memory. Nothing renders wider than the bound, so the visual result is unchanged.
///     Every image decode belongs here: the background wallpaper, which is the user's own file,
///     and asset previews, which come out of downloaded packages and are not trusted at all.
/// </summary>
internal static class BoundedImageDecoder
{
    internal const int MaxDecodeWidth = 3840;

    public static Bitmap DecodeFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Decode(stream);
    }

    public static Bitmap Decode(Stream stream)
    {
        var boundedWidth = SelectBoundedDecodeWidth(TryProbeImageWidth(stream), MaxDecodeWidth);
        return boundedWidth is { } width
            ? Bitmap.DecodeToWidth(stream, width)
            : new Bitmap(stream);
    }

    internal static int? SelectBoundedDecodeWidth(int? sourceWidth, int maxWidth)
    {
        return sourceWidth > maxWidth ? maxWidth : null;
    }

    internal static int? TryProbeImageWidth(Stream stream)
    {
        if (!stream.CanSeek)
            return null;

        var position = stream.Position;
        try
        {
            var info = ImageInfo.FromStream(stream);
            return info?.Width;
        }
        catch
        {
            return null;
        }
        finally
        {
            try
            {
                stream.Position = position;
            }
            catch
            {
                // If the stream refuses to rewind, the caller's decode fails and is handled there.
            }
        }
    }
}
