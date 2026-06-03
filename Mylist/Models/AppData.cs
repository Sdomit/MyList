using System.Collections.ObjectModel;

namespace MyList.Models;

public sealed class AppData
{
    public int SchemaVersion { get; set; } = 1;
    public AppSettings AppSettings { get; set; } = new();
    public ObservableCollection<CollectionModel> Collections { get; set; } = new();
    public ObservableCollection<ItemModel> Items { get; set; } = new();
    public ObservableCollection<ClipboardImportHistoryEntry> ClipboardImportHistory { get; set; } = new();
    public ObservableCollection<string> MigrationLogs { get; set; } = new();
}
