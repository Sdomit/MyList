using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace MyList.Views;

public partial class ConfirmWindow : Window
{
    // Match the quick menu (MiniLauncherWindow) exactly.
    private const int CornerRadiusDip = 50;

    private ConfirmWindow(string title, string message, string confirmLabel)
    {
        InitializeComponent();
        TitleText.Text = title;
        MessageText.Text = message;
        ConfirmButton.Content = confirmLabel;
        SourceInitialized += (_, _) => ApplyRoundedRegion();
        SizeChanged += (_, _) => ApplyRoundedRegion();
    }

    /// <summary>Themed Yes/No confirm. Returns true when the user confirms.</summary>
    public static bool Show(string title, string message, string confirmLabel = "Delete")
    {
        var dialog = new ConfirmWindow(title, message, confirmLabel)
        {
            Owner = System.Windows.Application.Current?.Windows
                .OfType<Window>()
                .FirstOrDefault(w => w.IsActive && w is not ConfirmWindow)
                ?? System.Windows.Application.Current?.MainWindow,
        };

        if (dialog.Owner is null)
        {
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        return dialog.ShowDialog() == true;
    }

    private void OnConfirm(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    // Shape the HWND itself so the dialog is truly rounded — identical mechanism
    // to MiniLauncherWindow (opaque window + native region), so no square edge.
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
