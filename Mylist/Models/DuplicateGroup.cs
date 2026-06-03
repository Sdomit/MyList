using System.Collections.ObjectModel;

namespace MyList.Models;

public sealed class DuplicateGroup
{
    public string PathKey { get; set; } = string.Empty;
    public string DisplayPath { get; set; } = string.Empty;
    public ObservableCollection<ItemModel> Items { get; set; } = new();
    public ItemModel? SelectedSurvivor { get; set; }
}
