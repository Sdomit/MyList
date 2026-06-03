using System.Collections.Generic;

namespace MyList.Models;

public enum AppCommandType
{
    ExplorerAddToMtab = 1
}

public sealed class AppCommandPayload
{
    public AppCommandType CommandType { get; set; } = AppCommandType.ExplorerAddToMtab;
    public string Source { get; set; } = "ExplorerContextMenu";
    public List<string> Paths { get; set; } = new();
}

