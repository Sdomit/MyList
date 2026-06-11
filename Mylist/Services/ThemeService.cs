using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Application = System.Windows.Application;
using MyList.Models;

namespace MyList.Services;

public sealed class ThemeService
{
    private const int DwmUseImmersiveDarkMode = 20;
    private const int DwmUseImmersiveDarkModeLegacy = 19;
    private const int DwmSystemBackdropType = 38;
    private const int DwmBackdropMica = 2;

    private static ThemeMode _currentMode = ThemeMode.Light;
    private static AccentPalette _currentAccent = AccentPalette.Blue;
    private static bool _windowThemeHandlerRegistered;

    public ThemeService()
    {
        EnsureWindowThemeHandlerRegistered();
    }

    public void ApplyTheme(ThemeMode mode, AccentPalette accent)
    {
        var app = Application.Current;
        if (app is null)
        {
            return;
        }

        _currentMode = mode;
        _currentAccent = accent;

        var source = $"Resources/Colors.{mode}.{accent}.xaml";

        var dictionaries = app.Resources.MergedDictionaries;
        var existingThemeDictionaries = dictionaries
            .Where(IsThemeDictionary)
            .ToList();

        foreach (var dictionary in existingThemeDictionaries)
        {
            dictionaries.Remove(dictionary);
        }

        var themeDictionary = new ResourceDictionary { Source = new Uri(source, UriKind.Relative) };
        var tokenIndex = dictionaries
            .Select((dictionary, index) => new { dictionary, index })
            .FirstOrDefault(entry => IsTokenDictionary(entry.dictionary))
            ?.index ?? -1;
        dictionaries.Insert(Math.Min(tokenIndex + 1, dictionaries.Count), themeDictionary);

        // Rebuild the brush layer so already-loaded windows pick up the swapped colors.
        // The Token.*Brush definitions resolve their Color via DynamicResource against the
        // sibling color dictionary; an already-instantiated brush does not re-resolve when
        // only that sibling is swapped, so a runtime theme change would otherwise be a no-op
        // (startup works only because ApplyTheme runs before any window is realized).
        foreach (var brushDictionary in dictionaries.Where(IsTokenDictionary).ToList())
        {
            var brushIndex = dictionaries.IndexOf(brushDictionary);
            if (brushIndex < 0 || brushDictionary.Source is null)
            {
                continue;
            }

            dictionaries.RemoveAt(brushIndex);
            dictionaries.Insert(brushIndex, new ResourceDictionary { Source = brushDictionary.Source });
        }

        foreach (Window window in app.Windows)
        {
            ApplyWindowFrameTheme(window, mode);
        }
    }

    private static bool IsThemeDictionary(ResourceDictionary dictionary)
    {
        var source = dictionary.Source?.OriginalString;
        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        return source.Replace('\\', '/')
            .Contains("Resources/Colors.", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTokenDictionary(ResourceDictionary dictionary)
    {
        var source = dictionary.Source?.OriginalString;
        return !string.IsNullOrWhiteSpace(source) &&
               source.Replace('\\', '/').Contains("Themes/Tokens.xaml", StringComparison.OrdinalIgnoreCase);
    }

    private static void EnsureWindowThemeHandlerRegistered()
    {
        if (_windowThemeHandlerRegistered)
        {
            return;
        }

        EventManager.RegisterClassHandler(
            typeof(Window),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnWindowLoaded),
            true);

        _windowThemeHandlerRegistered = true;
    }

    private static void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is Window window)
        {
            ApplyWindowFrameTheme(window, _currentMode);
        }
    }

    private static void OnWindowSourceInitialized(object? sender, EventArgs e)
    {
        if (sender is not Window window)
        {
            return;
        }

        window.SourceInitialized -= OnWindowSourceInitialized;
        ApplyWindowFrameTheme(window, _currentMode);
    }

    private static void ApplyWindowFrameTheme(Window window, ThemeMode mode)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            window.SourceInitialized -= OnWindowSourceInitialized;
            window.SourceInitialized += OnWindowSourceInitialized;
            return;
        }

        var useDarkMode = mode == ThemeMode.Dark ? 1 : 0;
        _ = DwmSetWindowAttribute(handle, DwmUseImmersiveDarkMode, ref useDarkMode, sizeof(int));
        _ = DwmSetWindowAttribute(handle, DwmUseImmersiveDarkModeLegacy, ref useDarkMode, sizeof(int));
        var backdropType = DwmBackdropMica;
        _ = DwmSetWindowAttribute(handle, DwmSystemBackdropType, ref backdropType, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);
}
