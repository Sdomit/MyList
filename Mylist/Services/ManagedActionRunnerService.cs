using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using MyList.Models;

namespace MyList.Services;

public sealed class ManagedActionRunnerService
{
    private readonly LogService _log = LogService.Instance;

    public string AppDataDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MyList");

    public string RuntimeDirectory => Path.Combine(AppDataDirectory, "action-runtime");

    public bool TryRun(ItemModel item, out string error)
    {
        error = string.Empty;

        if (item is null || !item.IsActionItem || string.IsNullOrWhiteSpace(item.ActionContent))
        {
            error = "Action item is empty.";
            return false;
        }

        try
        {
            Directory.CreateDirectory(RuntimeDirectory);
            var runtimePath = WriteRuntimeScript(item);
            var startInfo = BuildStartInfo(item, runtimePath);
            Process.Start(startInfo)?.Dispose();
            return true;
        }
        catch (Exception ex)
        {
            _log.Log(ex, $"Failed to run action item '{item.Name}'.");
            error = ex.Message;
            return false;
        }
    }

    private string WriteRuntimeScript(ItemModel item)
    {
        var extension = item.ActionKind switch
        {
            ActionKind.Command => ".cmd",
            ActionKind.Batch => ".bat",
            ActionKind.PowerShell => ".ps1",
            _ => ".txt"
        };

        var runtimePath = Path.Combine(RuntimeDirectory, $"{item.Id:N}{extension}");
        var content = NormalizeLineEndings(item.ActionContent);
        File.WriteAllText(runtimePath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return runtimePath;
    }

    private static ProcessStartInfo BuildStartInfo(ItemModel item, string runtimePath)
    {
        var workingDirectory = string.IsNullOrWhiteSpace(item.LaunchProfile.WorkingDirectory)
            ? Environment.CurrentDirectory
            : item.LaunchProfile.WorkingDirectory;

        if (item.ActionKind == ActionKind.PowerShell)
        {
            var arguments = $"-ExecutionPolicy Bypass -File \"{runtimePath}\"";
            return new ProcessStartInfo("powershell.exe", arguments)
            {
                UseShellExecute = true,
                WorkingDirectory = workingDirectory,
                Verb = item.LaunchProfile.RunAsAdmin ? "runas" : string.Empty
            };
        }

        return new ProcessStartInfo(runtimePath)
        {
            UseShellExecute = true,
            WorkingDirectory = workingDirectory,
            Verb = item.LaunchProfile.RunAsAdmin ? "runas" : "open"
        };
    }

    private static string NormalizeLineEndings(string content)
    {
        return content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Replace("\n", Environment.NewLine, StringComparison.Ordinal);
    }
}
