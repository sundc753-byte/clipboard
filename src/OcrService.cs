using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace ClipboardApp;

/// <summary>Text recognition using the OCR engine built into Windows 10/11.</summary>
internal static class OcrService
{
    private static OcrEngine? _engine;
    private static bool _initialized;

    private static OcrEngine? Engine()
    {
        if (_initialized) return _engine;
        _initialized = true;
        _engine = OcrEngine.TryCreateFromUserProfileLanguages();
        foreach (var tag in new[] { "ko", "en-US" })
        {
            if (_engine is not null) break;
            _engine = OcrEngine.TryCreateFromLanguage(new Language(tag));
        }
        return _engine;
    }

    /// <summary>Returns recognized text, or an empty string when nothing/no engine is available.</summary>
    public static async Task<string> RecognizeAsync(string pngPath)
    {
        try
        {
            var engine = Engine();
            if (engine is null) return "";

            using var stream = File.OpenRead(pngPath);
            var decoder = await BitmapDecoder.CreateAsync(stream.AsRandomAccessStream());
            using var bitmap = await decoder.GetSoftwareBitmapAsync(
                BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);

            if (bitmap.PixelWidth > OcrEngine.MaxImageDimension || bitmap.PixelHeight > OcrEngine.MaxImageDimension)
                return "";

            var result = await engine.RecognizeAsync(bitmap);
            return result.Text.Trim();
        }
        catch (Exception)
        {
            return "";
        }
    }
}
