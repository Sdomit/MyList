using System;
using System.Collections.ObjectModel;
using System.Linq;
using MyList.Helpers;
using MyList.Models;

namespace MyList.ViewModels;

public sealed class EditItemViewModel : ViewModelBase
{
    private readonly ItemModel _item;
    private string _name;
    private string _path;
    private ItemType _type;
    private string _tags;
    private string? _iconPath;
    private bool _useSystemIcon;

    public EditItemViewModel(ItemModel item)
    {
        _item = item;
        _name = item.Name;
        _path = item.Path;
        _type = item.Type;
        _tags = string.Join(", ", item.Tags);
        _iconPath = item.IconPath;
        _useSystemIcon = item.UseSystemIcon;
        ItemTypes = new ObservableCollection<ItemType>(Enum.GetValues<ItemType>());
    }

    public ObservableCollection<ItemType> ItemTypes { get; }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string Path
    {
        get => _path;
        set => SetProperty(ref _path, value);
    }

    public ItemType Type
    {
        get => _type;
        set => SetProperty(ref _type, value);
    }

    public string Tags
    {
        get => _tags;
        set => SetProperty(ref _tags, value);
    }

    public string? IconPath
    {
        get => _iconPath;
        set => SetProperty(ref _iconPath, value);
    }

    public bool UseSystemIcon
    {
        get => _useSystemIcon;
        set => SetProperty(ref _useSystemIcon, value);
    }

    public void Apply()
    {
        _item.Name = Name;
        _item.Path = Path;
        _item.Type = Type;
        _item.IconPath = IconPath;
        _item.UseSystemIcon = UseSystemIcon;
        _item.Tags.Clear();
        foreach (var tag in Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            _item.Tags.Add(tag);
        }
    }
}
