using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace ClipboardApp;

public partial class App : System.Windows.Application
{
    private Mutex? _singleInstance;
    private HistoryStore _store = null!;
    private MainWindow _window = null!;
    private KeyboardHook? _hook;
    private ClipboardMonitor? _monitor;
    private System.Windows.Forms.NotifyIcon? _tray;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstance = new Mutex(true, @"Local\ClipboardApp.SingleInstance", out var createdNew);
        if (!createdNew)
        {
            Shutdown();
            return;
        }

        _store = new HistoryStore();
        _window = new MainWindow(_store);

        // Run at sign-in by default; the tray menu can turn it off.
        var marker = Path.Combine(_store.DataDir, "initialized");
        if (!File.Exists(marker))
        {
            Autostart.Set(true);
            File.WriteAllText(marker, "");
        }

        _monitor = new ClipboardMonitor();
        _monitor.Changed += () => Dispatcher.BeginInvoke(OnClipboardChanged);

        try
        {
            _hook = new KeyboardHook();
            _hook.WinVPressed += () => Dispatcher.BeginInvoke(_window.ShowPopup);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Win+V 단축키를 등록하지 못했습니다.\n트레이 아이콘을 더블클릭하면 열 수 있습니다.\n\n{ex.Message}",
                "클립보드", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        BuildTray();
    }

    private void BuildTray()
    {
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("열기 (Win+V)", null, (_, _) => _window.ShowPopup());
        menu.Items.Add("기록 모두 지우기", null, (_, _) => _store.Clear());

        var auto = new System.Windows.Forms.ToolStripMenuItem("Windows 시작 시 자동 실행")
        {
            Checked = Autostart.IsEnabled,
            CheckOnClick = true,
        };
        auto.CheckedChanged += (_, _) => Autostart.Set(auto.Checked);
        menu.Items.Add(auto);

        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("종료", null, (_, _) => Shutdown());

        _tray = new System.Windows.Forms.NotifyIcon
        {
            Icon = CreateIcon(),
            Text = "클립보드 (Win+V)",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _tray.MouseDoubleClick += (_, a) =>
        {
            if (a.Button == System.Windows.Forms.MouseButtons.Left) _window.ShowPopup();
        };
    }

    private static Icon CreateIcon()
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.FillRectangle(new SolidBrush(Color.FromArgb(0x1E, 0x7A, 0xD6)), 5, 5, 22, 25);
            g.FillRectangle(Brushes.White, 9, 11, 14, 16);
            g.FillRectangle(new SolidBrush(Color.FromArgb(0x33, 0x33, 0x33)), 11, 2, 10, 7);
        }
        return Icon.FromHandle(bmp.GetHicon());
    }

    private async void OnClipboardChanged()
    {
        // The clipboard is often still locked by the copying app for a moment.
        await Task.Delay(40);
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                await ReadClipboard();
                return;
            }
            catch (COMException)
            {
                await Task.Delay(80);
            }
        }
    }

    private async Task ReadClipboard()
    {
        // Password managers flag their copies so they stay out of history.
        if (Clipboard.ContainsData("ExcludeClipboardContentFromMonitorProcessing")) return;

        if (Clipboard.ContainsText())
        {
            var text = Clipboard.GetText();
            if (!string.IsNullOrWhiteSpace(text)) _store.AddText(text);
        }
        else if (Clipboard.ContainsImage())
        {
            if (Clipboard.GetImage() is not BitmapSource img) return;
            var (item, isNew) = _store.AddImage(img);
            if (isNew && item.ImagePath is not null)
            {
                var text = await OcrService.RecognizeAsync(item.ImagePath);
                item.OcrText = text;
                _store.Save();
            }
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _hook?.Dispose();
        _monitor?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
