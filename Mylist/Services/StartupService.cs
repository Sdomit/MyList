using System;
using System.Diagnostics;
using Microsoft.Win32;

namespace MyList.Services;

public sealed class StartupService
{
    private const string RunKey = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";
    private const string AppName = "MyList";

    private readonly LogService _log = LogService.Instance;

    public void SetStartup(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, true);
            if (key is null)
            {
                _log.Log("Cannot update startup entry: Run registry key unavailable.");
                return;
            }

            if (enable)
            {
                var path = Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrWhiteSpace(path))
                {
                    _log.Log("Cannot enable startup: executable path unavailable.");
                    return;
                }

                key.SetValue(AppName, $"\"{path}\"");
            }
            else
            {
                key.DeleteValue(AppName, false);
            }
        }
        catch (Exception ex)
        {
            _log.Log(ex, $"Failed to {(enable ? "enable" : "disable")} startup entry.");
        }
    }
}
