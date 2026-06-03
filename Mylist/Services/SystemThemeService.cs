using System;
using Microsoft.Win32;
using MyList.Models;

namespace MyList.Services;

public sealed class SystemThemeService : IDisposable
{
    private UserPreferenceChangedEventHandler? _handler;
    private Action<ThemeMode>? _callback;

    public ThemeMode GetCurrentTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                "Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            if (value is int flag && flag == 0)
            {
                return ThemeMode.Dark;
            }
        }
        catch
        {
            // ignore
        }

        return ThemeMode.Light;
    }

    public void StartWatching(Action<ThemeMode> onChange)
    {
        StopWatching();
        _callback = onChange;
        _handler = (_, _) =>
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher is null || dispatcher.CheckAccess())
            {
                _callback?.Invoke(GetCurrentTheme());
            }
            else
            {
                dispatcher.InvokeAsync(() => _callback?.Invoke(GetCurrentTheme()));
            }
        };
        SystemEvents.UserPreferenceChanged += _handler;
    }

    public void StopWatching()
    {
        if (_handler is not null)
        {
            SystemEvents.UserPreferenceChanged -= _handler;
            _handler = null;
        }
    }

    public void Dispose()
    {
        StopWatching();
    }
}
