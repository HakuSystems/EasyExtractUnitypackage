using FontStyle = System.Drawing.FontStyle;

namespace EasyExtract.Utilities;

public static class CodeToImageConverter
{
    public static BitmapImage? ConvertCodeToImage(string code)
    {
        try
        {
            using var bitmap = new Bitmap(800, 600);
            using var graphics = Graphics.FromImage(bitmap);
            graphics.Clear(Color.Transparent);
            graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            Font font;
            try
            {
                font = new Font("SegoeFluentIcons", 30, FontStyle.Bold, GraphicsUnit.Pixel);
            }
            catch
            {
                font = new Font("Segoe UI", 30, FontStyle.Bold, GraphicsUnit.Pixel);
            }

            using var brush = new SolidBrush(Color.White);

            var layoutRect = new RectangleF(10, 10, 780, 580);
            graphics.DrawString(code, font, brush, layoutRect);

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
        catch (Exception ex)
        {
            Console.WriteLine($"Error in ConvertCodeToImage: {ex.Message}");
            return null;
        }
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
                    throw; // Re-throw if max retries reached
                Task.Delay(delay).Wait();
            }
    }
}