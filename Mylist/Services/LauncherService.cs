using System;
using System.Diagnostics;
using System.IO;
using MyList.Models;

namespace MyList.Services;

public sealed class LauncherService
{
    public void Open(ItemModel item)
    {
        if (string.IsNullOrWhiteSpace(item.Path))
        {
            return;
        }

        if (item.Type == ItemType.Folder)
        {
            OpenFolder(item.Path);
            return;
        }

        var startInfo = BuildStartInfo(item);
        Process.Start(startInfo)?.Dispose();
    }

    public void OpenPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true })?.Dispose();
    }

    public void OpenNewWindow(ItemModel item)
    {
        if (string.IsNullOrWhiteSpace(item.Path))
        {
            return;
        }

        if (item.Type == ItemType.Folder)
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/n,\"{item.Path}\"") { UseShellExecute = true })?.Dispose();
            return;
        }

        Open(item);
    }

    public void OpenInTerminal(ItemModel item)
    {
        var targetPath = item.Type == ItemType.Folder
            ? item.Path
            : Path.GetDirectoryName(item.Path) ?? Environment.CurrentDirectory;

        if (string.IsNullOrWhiteSpace(targetPath))
        {
            return;
        }

        var profile = item.LaunchProfile.TerminalProfile;
        if (profile == TerminalProfile.Default)
        {
            profile = IsWindowsTerminalAvailable() ? TerminalProfile.WindowsTerminal : TerminalProfile.Cmd;
        }

        switch (profile)
        {
            case TerminalProfile.WindowsTerminal:
                Process.Start(new ProcessStartInfo("wt", $"-d \"{targetPath}\"") { UseShellExecute = true })?.Dispose();
                return;
            case TerminalProfile.PowerShell:
                var escapedPath = targetPath.Replace("'", "''");
                var command = $"-NoExit -Command \"Set-Location -LiteralPath '{escapedPath}'\"";
                Process.Start(new ProcessStartInfo("powershell.exe", command) { UseShellExecute = true })?.Dispose();
                return;
            case TerminalProfile.Cmd:
                Process.Start(new ProcessStartInfo("cmd.exe", $"/K cd /d \"{targetPath}\"") { UseShellExecute = true })?.Dispose();
                return;
            default:
                Process.Start(new ProcessStartInfo("cmd.exe", $"/K cd /d \"{targetPath}\"") { UseShellExecute = true })?.Dispose();
                return;
        }
    }

    private static ProcessStartInfo BuildStartInfo(ItemModel item)
    {
        var info = new ProcessStartInfo(item.Path)
        {
            UseShellExecute = true
        };

        var profile = item.LaunchProfile ?? new ItemLaunchProfile();
        if (!string.IsNullOrWhiteSpace(profile.Arguments))
        {
            info.Arguments = profile.Arguments;
        }

        var workingDirectory = profile.WorkingDirectory;
        if (string.IsNullOrWhiteSpace(workingDirectory))
        {
            workingDirectory = Path.GetDirectoryName(item.Path) ?? Environment.CurrentDirectory;
        }

        if (!string.IsNullOrWhiteSpace(workingDirectory))
        {
            info.WorkingDirectory = workingDirectory;
        }

        if (profile.RunAsAdmin)
        {
            info.Verb = "runas";
        }

        return info;
    }

    private static void OpenFolder(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
                Verb = "open"
            })?.Dispose();
        }
        catch
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true })?.Dispose();
        }
    }

    private static bool IsWindowsTerminalAvailable()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var wtPath = Path.Combine(localAppData, "Microsoft", "WindowsApps", "wt.exe");
        return File.Exists(wtPath);
    }
}
