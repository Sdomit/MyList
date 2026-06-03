using System.Collections.Generic;

namespace MyList.Models;

public sealed class ExplorerMtabOpenResult
{
    public int RequestedCount { get; set; }
    public int OpenedAsTabsCount { get; set; }
    public int OpenedAsWindowsCount { get; set; }
    public bool TabsSupported { get; set; }
    public List<string> FailedPaths { get; set; } = new();
    public int CleanedHomeTabsCount { get; set; }
    public List<string> FallbackPaths { get; set; } = new();

    public string BuildSummary()
    {
        return $"Requested: {RequestedCount}, tabs: {OpenedAsTabsCount}, windows: {OpenedAsWindowsCount}, cleaned: {CleanedHomeTabsCount}, fallback: {FallbackPaths.Count}, failed: {FailedPaths.Count}";
    }
}
