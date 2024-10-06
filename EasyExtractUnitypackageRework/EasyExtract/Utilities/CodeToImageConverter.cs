using FontStyle = System.Drawing.FontStyle;

namespace EasyExtract.Utilities;

public class CodeToImageConverter
{
    /// <summary>
    ///     Converts the given code to an image.
    /// </summary>
    /// <param name="code">The code to convert to an image.</param>
    /// <returns>
    ///     A BitmapImage object representing the converted code as an image, or null if the code cannot be converted.
    /// </returns>
    public static BitmapImage? ConvertCodeToImage(string code)
    {
        using var bitmap = new Bitmap(800, 600); // width, height
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Transparent); // background color
        using var font = new Font("SegoeFluentIcons", 30, FontStyle.Bold, GraphicsUnit.Pixel);
        graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        using var brush = new SolidBrush(Color.White); // foreground color
        graphics.DrawString(code, font, brush, new PointF(10, 10));

        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);

        stream.Position = 0;
        var bitmapImage = new BitmapImage();
        bitmapImage.BeginInit();
        bitmapImage.StreamSource = stream;
        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
        bitmapImage.EndInit();
        bitmapImage.Freeze();

        return bitmapImage;
    }

    public static void SaveImageToFile(BitmapImage image, string path)
    {
        const int maxRetries = 5;
        const int delay = 200; // milliseconds
        for (var i = 0; i < maxRetries; i++)
            try
            {
                using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(image));
                encoder.Save(fileStream);
                break; // Exit loop if save is successful
            }
            catch (IOException)
            {
                if (i == maxRetries - 1)
                    throw; // Re-throw the exception if max retries are reached

                Task.Delay(delay).Wait(); // Wait for a while before retrying
            }
    }
}