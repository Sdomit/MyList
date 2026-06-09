using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace MyList.Helpers;

/// <summary>
/// Win11 22H2+ system backdrop wrapper. DwmSetWindowAttribute calls fail
/// silently on older Windows; caller checks the return to know whether to
/// keep the fallback solid background.
/// </summary>
public static class MicaHelper
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;

    // DWM_SYSTEMBACKDROP_TYPE enum: 0=Auto, 1=None, 2=MainWindow (Mica),
    // 3=TransientWindow (Acrylic), 4=TabbedWindow (Mica Alt).
    private const int DWMSBT_MAINWINDOW = 2;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    public static bool TryApply(Window window, bool darkMode)
    {
        if (window is null)
        {
            return false;
        }

        var hwnd = new WindowInteropHelper(window).EnsureHandle();
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        var dark = darkMode ? 1 : 0;
        var darkResult = DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));

        var backdrop = DWMSBT_MAINWINDOW;
        var backdropResult = DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));

        return darkResult == 0 && backdropResult == 0;
    }
}
