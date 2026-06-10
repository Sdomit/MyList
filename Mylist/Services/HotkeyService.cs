using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using MyList.Models;
using MyList.ViewModels;

namespace MyList.Services;

public sealed class HotkeyService : IDisposable
{
    private readonly Dictionary<int, Action> _callbacks = new();
    private readonly LogService _log = LogService.Instance;
    private HwndSource? _source;
    private int _currentId = 1000;
    private Window? _window;
    private readonly List<int> _primaryHotkeyIds = new();
    private readonly List<(HotkeySettings Settings, Action Callback)> _persistentSecondaryHotkeys = new();
    private readonly Dictionary<string, (int Id, Action Callback)> _namedSecondaryHotkeys = new();

    public void Initialize(Window window, MainViewModel viewModel)
    {
        _window = window;
        var helper = new WindowInteropHelper(window);
        _source = HwndSource.FromHwnd(helper.Handle);
        _source.AddHook(WndProc);

        RegisterHotkeyWithFallback(viewModel.Settings.AppSettings.GlobalHotkey, () => BringWindowToFront(window));
        viewModel.HotkeyChanged += (_, settings) => UpdateHotkey(settings);
    }

    public void UpdateHotkey(HotkeySettings settings)
    {
        if (_window is null)
        {
            return;
        }

        UnregisterPrimary();
        RegisterHotkeyWithFallback(settings, () => BringWindowToFront(_window));
        ReregisterSecondaryHotkeys();
    }

    public void RegisterSecondaryHotkey(HotkeySettings settings, Action callback)
    {
        _persistentSecondaryHotkeys.Add((settings, callback));
        var result = RegisterHotkey(settings, callback);
        if (!result.success)
        {
            _log.Log($"Failed to register secondary hotkey {settings} (Win32: {result.errorCode}).");
        }
    }

    public void RegisterOrUpdateNamedSecondaryHotkey(string name, HotkeySettings settings, Action callback)
    {
        UnregisterNamedSecondaryHotkey(name);

        var source = _source;
        if (source is null || source.Handle == IntPtr.Zero || settings.Key == Key.None)
        {
            _log.Log($"Cannot register named secondary hotkey '{name}': source not ready or key unset.");
            return;
        }

        var id = _currentId++;
        var modifiers = (uint)settings.Modifiers;
        var key = (uint)KeyInterop.VirtualKeyFromKey(settings.Key);
        if (RegisterHotKey(source.Handle, id, modifiers, key))
        {
            _callbacks[id] = callback;
            _namedSecondaryHotkeys[name] = (id, callback);
        }
        else
        {
            var error = Marshal.GetLastWin32Error();
            _log.Log($"Failed to register named secondary hotkey '{name}' {settings} (Win32: {error}).");
        }
    }

    public void UnregisterNamedSecondaryHotkey(string name)
    {
        if (!_namedSecondaryHotkeys.TryGetValue(name, out var entry))
        {
            return;
        }

        var source = _source;
        if (source is not null)
        {
            UnregisterHotKey(source.Handle, entry.Id);
        }

        _callbacks.Remove(entry.Id);
        _namedSecondaryHotkeys.Remove(name);
    }

    private void ReregisterSecondaryHotkeys()
    {
        foreach (var (settings, callback) in _persistentSecondaryHotkeys)
        {
            RegisterHotkey(settings, callback);
        }
    }

    private void RegisterHotkeyWithFallback(HotkeySettings settings, Action callback)
    {
        RuntimeStatus.HotkeyRegistered = false;
        RuntimeStatus.HotkeyStatusMessage = "Registering hotkey...";
        RuntimeStatus.HotkeyErrorCode = 0;

        var candidates = new[]
        {
            settings,
            new HotkeySettings { Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift, Key = Key.Q },
            new HotkeySettings { Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Alt, Key = Key.M }
        }
        .DistinctBy(candidate => (candidate.Modifiers, candidate.Key))
        .ToArray();

        foreach (var candidate in candidates)
        {
            var result = RegisterHotkey(candidate, callback);
            if (result.success)
            {
                RuntimeStatus.HotkeyRegistered = true;
                RuntimeStatus.EffectiveHotkey = candidate;
                RuntimeStatus.HotkeyStatusMessage = candidate.Key == settings.Key && candidate.Modifiers == settings.Modifiers
                    ? $"Hotkey active: {candidate}"
                    : $"Requested hotkey busy. Fallback active: {candidate}";
                return;
            }

            if (result.errorCode != ERROR_HOTKEY_ALREADY_REGISTERED)
            {
                RuntimeStatus.HotkeyErrorCode = result.errorCode;
            }
        }

        RuntimeStatus.HotkeyStatusMessage = $"Failed to register global hotkey (Win32: {RuntimeStatus.HotkeyErrorCode}).";
    }

    private (bool success, int errorCode) RegisterHotkey(HotkeySettings settings, Action callback)
    {
        var source = _source;
        if (source is null || source.Handle == IntPtr.Zero)
        {
            return (false, 0);
        }

        if (settings.Key == Key.None)
        {
            return (false, 0);
        }

        var id = _currentId++;
        var modifiers = (uint)settings.Modifiers;
        var key = (uint)KeyInterop.VirtualKeyFromKey(settings.Key);

        if (RegisterHotKey(source.Handle, id, modifiers, key))
        {
            _callbacks[id] = callback;
            _primaryHotkeyIds.Add(id);
            return (true, 0);
        }

        var error = Marshal.GetLastWin32Error();
        _log.Log($"Failed to register hotkey {settings} (Win32: {error}).");
        return (false, error);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_HOTKEY = 0x0312;
        if (msg == WM_HOTKEY)
        {
            var id = wParam.ToInt32();
            if (_callbacks.TryGetValue(id, out var callback))
            {
                callback();
                handled = true;
            }
        }

        return IntPtr.Zero;
    }

    private void BringWindowToFront(Window window)
    {
        if (!window.Dispatcher.CheckAccess())
        {
            window.Dispatcher.Invoke(() => BringWindowToFront(window));
            return;
        }

        if (window is Views.MainWindow mainWindow)
        {
            mainWindow.RestoreAndActivate();
            return;
        }

        if (!window.IsVisible)
        {
            window.Show();
        }

        var handle = new WindowInteropHelper(window).Handle;
        if (handle != IntPtr.Zero)
        {
            if (IsIconic(handle))
            {
                ShowWindow(handle, SW_RESTORE);
            }

            ShowWindow(handle, SW_SHOW);
            SetForegroundWindow(handle);
        }

        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        window.Activate();
        window.Topmost = true;
        window.Topmost = false;
        window.Focus();
    }

    private void UnregisterPrimary()
    {
        var source = _source;
        if (source is null)
        {
            return;
        }

        foreach (var id in _primaryHotkeyIds)
        {
            UnregisterHotKey(source.Handle, id);
            _callbacks.Remove(id);
        }

        _primaryHotkeyIds.Clear();
        RuntimeStatus.HotkeyRegistered = false;
    }

    private void UnregisterAll()
    {
        var source = _source;
        if (source is null)
        {
            return;
        }

        foreach (var id in _callbacks.Keys.ToList())
        {
            UnregisterHotKey(source.Handle, id);
        }
        _callbacks.Clear();
        _primaryHotkeyIds.Clear();
        RuntimeStatus.HotkeyRegistered = false;
    }

    public void Dispose()
    {
        UnregisterAll();
        if (_source is not null)
        {
            _source.RemoveHook(WndProc);
        }
    }

    private const int SW_SHOW = 5;
    private const int SW_RESTORE = 9;
    private const int ERROR_HOTKEY_ALREADY_REGISTERED = 1409;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);
}
