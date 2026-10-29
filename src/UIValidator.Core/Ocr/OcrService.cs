using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace UIValidator.Core.Ocr;

/// <summary>Thin wrapper over the built-in Windows OCR engine (Windows.Media.Ocr).</summary>
public sealed class OcrService
{
    private readonly OcrEngine _engine;

    private OcrService(OcrEngine engine) => _engine = engine;

    /// <summary>Creates the service, or returns null when no OCR language pack is installed.</summary>
    public static OcrService? TryCreate(string? languageTag)
    {
        try
        {
            var engine = string.IsNullOrWhiteSpace(languageTag)
                ? OcrEngine.TryCreateFromUserProfileLanguages()
                : OcrEngine.TryCreateFromLanguage(new Language(languageTag));
            return engine is null ? null : new OcrService(engine);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Recognizes the text inside <paramref name="region"/> of <paramref name="image"/>.</summary>
    public string Recognize(Bitmap image, Rectangle region)
    {
        region.Intersect(new Rectangle(0, 0, image.Width, image.Height));
        if (region.Width < 4 || region.Height < 4) return "";

        // Small UI text is recognized much better when upscaled.
        var factor = region.Height < 40 ? 3 : 1;
        using var crop = new Bitmap(region.Width * factor, region.Height * factor, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(crop))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(image, new Rectangle(0, 0, crop.Width, crop.Height), region, GraphicsUnit.Pixel);
        }

        return RecognizeAsync(crop).GetAwaiter().GetResult();
    }

    private async Task<string> RecognizeAsync(Bitmap bitmap)
    {
        using var png = new MemoryStream();
        bitmap.Save(png, ImageFormat.Png);

        using var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
        {
            writer.WriteBytes(png.ToArray());
            await writer.StoreAsync();
        }

        var decoder = await BitmapDecoder.CreateAsync(stream);
        using var software = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        var result = await _engine.RecognizeAsync(software);
        return result.Text ?? "";
    }
}
