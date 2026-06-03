namespace MyList.Models;

public enum TerminalProfile
{
    Default,
    WindowsTerminal,
    PowerShell,
    Cmd
}

public sealed class ItemLaunchProfile
{
    public string Arguments { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = string.Empty;
    public bool RunAsAdmin { get; set; }
    public TerminalProfile TerminalProfile { get; set; } = TerminalProfile.Default;
}
