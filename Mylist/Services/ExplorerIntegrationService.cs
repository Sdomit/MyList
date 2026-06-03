using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using MyList.Models;

namespace MyList.Services;

public sealed class ExplorerIntegrationService
{
    private const string DirectoryVerbKey = @"Software\Classes\Directory\shell\MyList.AddToMtab";
    private const string FolderVerbKey = @"Software\Classes\Folder\shell\MyList.AddToMtab";
    private const string VerbName = "Add to MyList Mtab...";
    private readonly LogService _log = LogService.Instance;

    public ExplorerIntegrationStatus GetStatus()
    {
        try
        {
            using var directoryCommand = Registry.CurrentUser.OpenSubKey($@"{DirectoryVerbKey}\command");
            using var folderCommand = Registry.CurrentUser.OpenSubKey($@"{FolderVerbKey}\command");
            var directoryValue = directoryCommand?.GetValue(string.Empty)?.ToString() ?? string.Empty;
            var folderValue = folderCommand?.GetValue(string.Empty)?.ToString() ?? string.Empty;
            var installed = !string.IsNullOrWhiteSpace(directoryValue)
                            && string.Equals(directoryValue, folderValue, StringComparison.Ordinal);

            return new ExplorerIntegrationStatus
            {
                IsInstalled = installed,
                StatusMessage = installed ? "Installed (current user)" : "Not installed",
                CommandValue = directoryValue
            };
        }
        catch (Exception ex)
        {
            _log.Log(ex, "Failed to read explorer integration status.");
            return new ExplorerIntegrationStatus
            {
                IsInstalled = false,
                StatusMessage = "Status check failed"
            };
        }
    }

    public ExplorerIntegrationStatus Install()
    {
        try
        {
            var command = BuildCommandValue();
            WriteVerb(DirectoryVerbKey, command);
            WriteVerb(FolderVerbKey, command);
            RefreshShellAssociations();

            RuntimeStatus.ExplorerMenuInstalled = true;
            RuntimeStatus.ExplorerMenuStatusMessage = "Installed";
            RuntimeStatus.ExplorerMenuLastOperationUtc = DateTime.UtcNow;

            _log.Log("Explorer integration installed.");
            return GetStatus();
        }
        catch (Exception ex)
        {
            RuntimeStatus.ExplorerMenuStatusMessage = "Install failed";
            RuntimeStatus.ExplorerMenuLastOperationUtc = DateTime.UtcNow;
            _log.Log(ex, "Failed to install explorer integration.");
            throw;
        }
    }

    public ExplorerIntegrationStatus Uninstall()
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(DirectoryVerbKey, false);
            Registry.CurrentUser.DeleteSubKeyTree(FolderVerbKey, false);
            RefreshShellAssociations();

            RuntimeStatus.ExplorerMenuInstalled = false;
            RuntimeStatus.ExplorerMenuStatusMessage = "Not installed";
            RuntimeStatus.ExplorerMenuLastOperationUtc = DateTime.UtcNow;

            _log.Log("Explorer integration uninstalled.");
            return GetStatus();
        }
        catch (Exception ex)
        {
            RuntimeStatus.ExplorerMenuStatusMessage = "Uninstall failed";
            RuntimeStatus.ExplorerMenuLastOperationUtc = DateTime.UtcNow;
            _log.Log(ex, "Failed to uninstall explorer integration.");
            throw;
        }
    }

    public ExplorerIntegrationStatus Repair()
    {
        return Install();
    }

    private static void WriteVerb(string keyPath, string command)
    {
        using var key = Registry.CurrentUser.CreateSubKey(keyPath, true);
        if (key is null)
        {
            throw new InvalidOperationException($"Could not create registry key: {keyPath}");
        }

        key.SetValue("MUIVerb", VerbName, RegistryValueKind.String);
        key.SetValue("MultiSelectModel", "Player", RegistryValueKind.String);
        key.SetValue("Icon", BuildIconValue(), RegistryValueKind.String);

        using var commandKey = Registry.CurrentUser.CreateSubKey($@"{keyPath}\command", true);
        if (commandKey is null)
        {
            throw new InvalidOperationException($"Could not create registry key: {keyPath}\\command");
        }

        commandKey.SetValue(string.Empty, command, RegistryValueKind.String);
    }

    private static string BuildCommandValue()
    {
        var exePath = ResolveMyListExecutablePath();
        return $"\"{exePath}\" --explorer-add-to-mtab -- %*";
    }

    private static string BuildIconValue()
    {
        var exePath = ResolveMyListExecutablePath();
        return $"\"{exePath}\",0";
    }

    private static string ResolveMyListExecutablePath()
    {
        var baseDirExe = Path.Combine(AppContext.BaseDirectory, "MyList.exe");
        if (File.Exists(baseDirExe))
        {
            return baseDirExe;
        }

        var current = Process.GetCurrentProcess().MainModule?.FileName;
        if (!string.IsNullOrWhiteSpace(current))
        {
            return current;
        }

        return baseDirExe;
    }

    private static void RefreshShellAssociations()
    {
        // SHCNE_ASSOCCHANGED + SHCNF_IDLIST
        SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero);
    }

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);
}

