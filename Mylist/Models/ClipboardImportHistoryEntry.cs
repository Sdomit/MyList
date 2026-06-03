using System;
using System.Collections.ObjectModel;

namespace MyList.Models;

public sealed class ClipboardImportHistoryEntry
{
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public string CollectionId { get; set; } = string.Empty;
    public string CollectionName { get; set; } = string.Empty;
    public ObservableCollection<string> Paths { get; set; } = new();
    public string Summary { get; set; } = string.Empty;
}
