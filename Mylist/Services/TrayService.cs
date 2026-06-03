using System;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using MyList.ViewModels;
using Application = System.Windows.Application;

namespace MyList.Services;

public sealed class TrayService : IDisposable
{
    private NotifyIcon? _notifyIcon;
    private ContextMenuStrip? _menu;
    private Window? _window;
    private Icon? _icon;

    public void Initialize(Window window, MainViewModel viewModel)
    {
        _window = window;
        _icon = LoadIcon();
        _notifyIcon = new NotifyIcon
        {
            Icon = _icon ?? SystemIcons.Application,
            Visible = true,
            Text = "MyList"
        };
        RuntimeStatus.TrayInitialized = true;
        RuntimeStatus.TrayVisible = true;

        var menu = _menu = new ContextMenuStrip();
        menu.Items.Add("Show/Hide", null, (_, _) => ToggleWindow());
        menu.Items.Add("Open Settings", null, (_, _) =>
        {
            ShowWindow();
            viewModel.IsSettingsOpen = true;
        });
        menu.Items.Add("Exit", null, (_, _) =>
        {
            if (Application.Current is App app)
            {
                app.RequestShutdown();
                return;
            }

            Application.Current.Shutdown();
        });
        _notifyIcon.ContextMenuStrip = menu;
        _notifyIcon.DoubleClick += (_, _) => ToggleWindow();
    }

    private void ToggleWindow()
    {
        if (_window is null)
        {
            return;
        }

        if (_window.IsVisible)
        {
            _window.Hide();
            RuntimeStatus.TrayVisible = false;
        }
        else
        {
            ShowWindow();
        }
    }

    private void ShowWindow()
    {
        if (_window is null)
        {
            return;
        }

        if (_window is Views.MainWindow mainWindow)
        {
            mainWindow.RestoreAndActivate();
        }
        else
        {
            _window.Show();
            _window.Activate();
        }

        RuntimeStatus.TrayVisible = true;
    }

    public void Dispose()
    {
        if (_notifyIcon is not null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }

        _menu?.Dispose();

        RuntimeStatus.TrayVisible = false;

        _icon?.Dispose();
    }

    private static Icon? LoadIcon()
    {
        try
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.ico");
            return File.Exists(path) ? new Icon(path) : null;
        }
        catch
        {
            return null;
        }
    }
}
