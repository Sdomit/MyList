namespace MyList.Models;

public sealed class ExplorerIntegrationStatus
{
    public bool IsInstalled { get; set; }
    public string StatusMessage { get; set; } = "Unknown";
    public string CommandValue { get; set; } = string.Empty;
}

