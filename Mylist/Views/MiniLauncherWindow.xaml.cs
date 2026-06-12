using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using MyList.ViewModels;
using Key = System.Windows.Input.Key;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Keyboard = System.Windows.Input.Keyboard;
using ModifierKeys = System.Windows.Input.ModifierKeys;

namespace MyList.Views;

public partial class MiniLauncherWindow : Window
{
    private const int CornerRadiusDip = 24;
    private readonly MiniLauncherViewModel _viewModel;

    public MiniLauncherWindow(MiniLauncherViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        PreviewKeyDown += OnPreviewKeyDown;
        SourceInitialized += (_, _) => ApplyRoundedRegion();
        SizeChanged += (_, _) => ApplyRoundedRegion();
    }

    public void Summon()
    {
        _viewModel.Refresh();
        if (!IsVisible)
        {
            Show();
        }

        PositionAtCursor();
        Activate();
        Focus();
        SearchBox.SelectAll();
        SearchBox.Focus();
    }

    // Spring up centred on the mouse, clamped to the cursor's monitor work area.
    private void PositionAtCursor()
    {
        var mouse = System.Windows.Forms.Control.MousePosition; // device pixels
        var source = PresentationSource.FromVisual(this);
        var dpiX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
        var dpiY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;
        var work = System.Windows.Forms.Screen.FromPoint(mouse).WorkingArea; // device pixels

        var left = mouse.X / dpiX - ActualWidth / 2;
        var top = mouse.Y / dpiY - ActualHeight / 2;

        var minLeft = work.Left / dpiX;
        var minTop = work.Top / dpiY;
        var maxLeft = work.Right / dpiX - ActualWidth;
        var maxTop = work.Bottom / dpiY - ActualHeight;

        Left = Math.Max(minLeft, Math.Min(left, maxLeft));
        Top = Math.Max(minTop, Math.Min(top, maxTop));
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Modifier combos are reserved (none used today); let them pass through.
        if (Keyboard.Modifiers is not ModifierKeys.None)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Enter:
                _viewModel.ActivateSelectedCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Escape:
                if (_viewModel.IsSearching)
                {
                    ClearSearch();
                }
                else if (_viewModel.IsInCollection)
                {
                    _viewModel.DrillUpCommand.Execute(null);
                }
                else
                {
                    _viewModel.CloseCommand.Execute(null);
                }

                e.Handled = true;
                break;

            case Key.Back:
                // Only hijack Backspace when the search box is empty, so it still
                // edits text mid-query. Empty + inside a collection → drill up.
                if (!_viewModel.IsSearching && _viewModel.IsInCollection)
                {
                    _viewModel.DrillUpCommand.Execute(null);
                    e.Handled = true;
                }

                break;

            case Key.Left:
                if (!_viewModel.IsSearching)
                {
                    _viewModel.RotateCommand.Execute("-1");
                    e.Handled = true;
                }

                break;

            case Key.Right:
                if (!_viewModel.IsSearching)
                {
                    _viewModel.RotateCommand.Execute("1");
                    e.Handled = true;
                }

                break;

            default:
                if (!_viewModel.IsSearching && TryGetDigit(e.Key, out var digit) && digit is >= 1 and <= 6)
                {
                    _viewModel.OpenIndexedCommand.Execute(digit.ToString());
                    e.Handled = true;
                }

                break;
        }
    }

    private void ClearSearch()
    {
        SearchBox.Clear();
        SearchBox.Focus();
    }

    private static bool TryGetDigit(Key key, out int digit)
    {
        digit = key switch
        {
            >= Key.D0 and <= Key.D9 => key - Key.D0,
            >= Key.NumPad0 and <= Key.NumPad9 => key - Key.NumPad0,
            _ => -1,
        };

        return digit >= 0;
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        Hide();
    }

    // Shape the HWND itself so the launcher is truly rounded instead of a
    // rectangular layered window with rounded content inside it.
    private void ApplyRoundedRegion()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        var cornerPreference = DWMWCP_ROUND;
        _ = DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPreference, sizeof(int));

        var source = PresentationSource.FromVisual(this);
        var dpiX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
        var dpiY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;
        var widthPx = Math.Max(1, (int)Math.Ceiling((ActualWidth > 0 ? ActualWidth : Width) * dpiX));
        var heightPx = Math.Max(1, (int)Math.Ceiling((ActualHeight > 0 ? ActualHeight : Height) * dpiY));
        var radiusPx = Math.Max(1, (int)Math.Ceiling(CornerRadiusDip * Math.Min(dpiX, dpiY)));

        var region = CreateRoundRectRgn(0, 0, widthPx + 1, heightPx + 1, radiusPx * 2, radiusPx * 2);
        if (region == IntPtr.Zero)
        {
            return;
        }

        if (SetWindowRgn(hwnd, region, true) == 0)
        {
            DeleteObject(region);
        }
    }

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateRoundRectRgn(int left, int top, int right, int bottom, int widthEllipse, int heightEllipse);

    [DllImport("user32.dll")]
    private static extern int SetWindowRgn(IntPtr hwnd, IntPtr region, bool redraw);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_ROUND = 2;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int attrSize);
}
