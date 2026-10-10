using System;
using System.ComponentModel;
using System.IO;
using System.Text.Json.Serialization;
using System.Windows.Media.Imaging;

namespace ClipboardApp;

public sealed class ClipItem : INotifyPropertyChanged
{
    public string Hash { get; set; } = "";
    public bool IsImage { get; set; }
    public string? Text { get; set; }
    public string? ImageFile { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public DateTime Time { get; set; } = DateTime.Now;

    private string? _ocrText;
    public string? OcrText
    {
        get => _ocrText;
        set
        {
            _ocrText = value;
            Raise(nameof(OcrText));
            Raise(nameof(Subtitle));
        }
    }

    /// <summary>Absolute path of the saved PNG; filled in by <see cref="HistoryStore"/>.</summary>
    [JsonIgnore] public string? ImagePath { get; set; }

    [JsonIgnore]
    public string Title => IsImage
        ? $"이미지 {Width}×{Height}"
        : Truncate(Text ?? "", 300);

    [JsonIgnore]
    public string Subtitle
    {
        get
        {
            if (!IsImage) return $"{(Text ?? "").Length}자";
            if (OcrText is null) return "텍스트 인식 중…";
            var t = OcrText.Replace('\n', ' ').Replace('\r', ' ').Trim();
            return t.Length == 0 ? "인식된 텍스트 없음" : "🔍 " + Truncate(t, 120);
        }
    }

    private BitmapImage? _thumb;

    [JsonIgnore]
    public BitmapImage? Thumbnail
    {
        get
        {
            if (_thumb is null && ImagePath is not null && File.Exists(ImagePath))
            {
                var b = new BitmapImage();
                b.BeginInit();
                b.UriSource = new Uri(ImagePath);
                b.DecodePixelWidth = 160;
                b.CacheOption = BitmapCacheOption.OnLoad;
                b.EndInit();
                b.Freeze();
                _thumb = b;
            }
            return _thumb;
        }
    }

    public bool Matches(string query) =>
        (Text?.Contains(query, StringComparison.CurrentCultureIgnoreCase) ?? false) ||
        (OcrText?.Contains(query, StringComparison.CurrentCultureIgnoreCase) ?? false);

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "…";

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
