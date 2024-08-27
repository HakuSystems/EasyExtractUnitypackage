using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Windows.Media.Imaging;

namespace EasyExtract.Config;

public class CodeToImageConverter
{
    public static BitmapImage? ConvertCodeToImage(string code)
    {
        // Create a bitmap with specific dimensions
        using var bitmap = new Bitmap(800, 600);

        // Create graphics object from bitmap
        using var graphics = Graphics.FromImage(bitmap);

        // Set the background color
        graphics.Clear(Color.White);

        // Create a font
        using var font = new Font("Consolas", 12, FontStyle.Regular, GraphicsUnit.Pixel);

        // Set the text rendering hints
        graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        // Create a brush
        using var brush = new SolidBrush(Color.Black);

        // Draw the code as text on the bitmap
        graphics.DrawString(code, font, brush, new PointF(10, 10));

        // Save the bitmap to a temporary memory stream
        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);

        // Convert stream to BitmapImage
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