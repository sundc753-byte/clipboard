using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace ClipboardApp;

public partial class MainWindow : Window
{
    private readonly HistoryStore _store;
    private readonly ICollectionView _view;
    private IntPtr _previousWindow;

    public MainWindow(HistoryStore store)
    {
        InitializeComponent();
        _store = store;
        _view = CollectionViewSource.GetDefaultView(store.Items);
        _view.Filter = o => string.IsNullOrWhiteSpace(SearchBox.Text) || ((ClipItem)o).Matches(SearchBox.Text.Trim());
        List.ItemsSource = _view;
    }

    /// <summary>Shows the popup near the bottom of the screen and focuses the search box.</summary>
    public void ShowPopup()
    {
        _previousWindow = Native.GetForegroundWindow();

        SearchBox.Text = "";
        var area = SystemParameters.WorkArea;
        Left = area.Left + (area.Width - Width) / 2;
        Top = area.Bottom - Height - 8;

        Show();
        Activate();
        SearchBox.Focus();
        if (List.Items.Count > 0)
        {
            List.SelectedIndex = 0;
            List.ScrollIntoView(List.SelectedItem);
        }
    }

    private void Window_Deactivated(object? sender, EventArgs e) => Hide();

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // 1..9, 0 select the 1st..10th item; once a search is being typed, digits go to the search box.
        if (SearchBox.Text.Length == 0 && Keyboard.Modifiers == ModifierKeys.None &&
            DigitIndex(e.Key) is int n && n < List.Items.Count)
        {
            List.SelectedIndex = n;
            List.ScrollIntoView(List.SelectedItem);
            e.Handled = true;
            return;
        }

        switch (e.Key)
        {
            case Key.Escape:
                Hide();
                e.Handled = true;
                break;
            case Key.Down:
                MoveSelection(1);
                e.Handled = true;
                break;
            case Key.Up:
                MoveSelection(-1);
                e.Handled = true;
                break;
            case Key.Enter:
                if (List.SelectedItem is ClipItem sel) _ = PasteAsync(sel);
                e.Handled = true;
                break;
            case Key.Delete when SearchBox.Text.Length == 0 || !SearchBox.IsKeyboardFocused:
                if (List.SelectedItem is ClipItem del) Delete(del);
                e.Handled = true;
                break;
        }
    }

    private static int? DigitIndex(Key key) => key switch
    {
        >= Key.D1 and <= Key.D9 => key - Key.D1,
        Key.D0 => 9,
        >= Key.NumPad1 and <= Key.NumPad9 => key - Key.NumPad1,
        Key.NumPad0 => 9,
        _ => null,
    };

    private void MoveSelection(int delta)
    {
        if (List.Items.Count == 0) return;
        var idx = Math.Clamp(List.SelectedIndex + delta, 0, List.Items.Count - 1);
        List.SelectedIndex = idx;
        List.ScrollIntoView(List.SelectedItem);
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _view.Refresh();
        if (List.Items.Count > 0) List.SelectedIndex = 0;
    }

    private void List_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject src) return;
        if (FindParent<Button>(src) is not null) return; // delete button handles itself
        if (ItemsControl.ContainerFromElement(List, src) is ListBoxItem { DataContext: ClipItem item })
            _ = PasteAsync(item);
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is ClipItem item) Delete(item);
    }

    private void Delete(ClipItem item)
    {
        var idx = List.SelectedIndex;
        _store.Remove(item);
        if (List.Items.Count > 0) List.SelectedIndex = Math.Min(idx, List.Items.Count - 1);
    }

    private async Task PasteAsync(ClipItem item)
    {
        if (!TrySetClipboard(item)) return;

        // The used item goes to the top of the history.
        _store.MoveToTop(item);

        Hide();
        if (_previousWindow != IntPtr.Zero) Native.SetForegroundWindow(_previousWindow);
        await Task.Delay(120);
        Native.SendCtrlV();
    }

    private static bool TrySetClipboard(ClipItem item)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                if (item.IsImage)
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(item.ImagePath!);
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    bmp.Freeze();
                    Clipboard.SetImage(bmp);
                }
                else
                {
                    Clipboard.SetText(item.Text ?? "");
                }
                return true;
            }
            catch (COMException)
            {
                System.Threading.Thread.Sleep(50);
            }
        }
        return false;
    }

    private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
    {
        for (var d = child; d is not null; d = System.Windows.Media.VisualTreeHelper.GetParent(d))
            if (d is T t) return t;
        return null;
    }
}

/// <summary>Maps a list position to its hotkey label: 0..8 → "1".."9", 9 → "0", beyond → blank.</summary>
public sealed class HotkeyNumberConverter : System.Windows.Data.IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
        value is int i && i is >= 0 and < 10 ? ((i + 1) % 10).ToString() : "";

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
        throw new NotSupportedException();
}
