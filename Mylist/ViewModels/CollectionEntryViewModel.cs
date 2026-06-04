using System.ComponentModel;
using System.Windows.Media;
using MyList.Models;

namespace MyList.ViewModels;

public sealed class CollectionEntryViewModel : ViewModelBase
{
    private readonly CollectionEntryModel _model;
    private ItemModel? _item;
    private int _shortcutIndex;

    public CollectionEntryViewModel(CollectionEntryModel model, ItemModel? item)
    {
        _model = model;
        UpdateItem(item);
    }

    // 1-based index among item-type entries in the collection (0 = no shortcut)
    public int ShortcutIndex
    {
        get => _shortcutIndex;
        set
        {
            if (SetProperty(ref _shortcutIndex, value))
            {
                OnPropertyChanged(nameof(ShortcutLabel));
            }
        }
    }

    // "⌘1"…"⌘9" for indices 1–9, empty string otherwise
    public string ShortcutLabel => _shortcutIndex >= 1 && _shortcutIndex <= 9
        ? $"⌘{_shortcutIndex}"
        : string.Empty;

    public CollectionEntryModel Model => _model;

    public ItemModel? Item => _item;

    public bool IsSeparator => _model.Kind == CollectionEntryKind.Separator;

    public string Name => IsSeparator ? "Separator" : _item?.Name ?? string.Empty;

    public string DisplayPath => IsSeparator ? string.Empty : _item?.DisplayPath ?? string.Empty;

    public ImageSource? Icon => _item?.Icon;

    public ItemHealthState HealthState => _item?.HealthState ?? ItemHealthState.Healthy;

    public bool IsMtab => _item?.IsMtab == true;

    public bool IsClipboardText => _item?.IsClipboardText == true;

    public bool IsClipboardImage => _item?.IsClipboardImage == true;

    public bool IsActionItem => _item?.IsActionItem == true;

    public ActionKind ActionKind => _item?.ActionKind ?? ActionKind.None;

    public string Path => _item?.Path ?? string.Empty;

    public ItemType Type => _item?.Type ?? ItemType.File;

    public bool IsFavorite => _item?.IsFavorite == true;

    public string SearchContent => _item?.SearchContent ?? string.Empty;

    public void UpdateItem(ItemModel? item)
    {
        if (ReferenceEquals(_item, item))
        {
            return;
        }

        if (_item is not null)
        {
            _item.PropertyChanged -= OnItemPropertyChanged;
        }

        _item = item;

        if (_item is not null)
        {
            _item.PropertyChanged += OnItemPropertyChanged;
        }

        RaiseAll();
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ItemModel.Name):
                OnPropertyChanged(nameof(Name));
                break;
            case nameof(ItemModel.DisplayPath):
                OnPropertyChanged(nameof(DisplayPath));
                break;
            case nameof(ItemModel.Icon):
                OnPropertyChanged(nameof(Icon));
                break;
            case nameof(ItemModel.HealthState):
                OnPropertyChanged(nameof(HealthState));
                break;
            case nameof(ItemModel.IsFavorite):
                OnPropertyChanged(nameof(IsFavorite));
                break;
            case nameof(ItemModel.IsClipboardText):
                OnPropertyChanged(nameof(IsClipboardText));
                break;
            case nameof(ItemModel.IsClipboardImage):
                OnPropertyChanged(nameof(IsClipboardImage));
                break;
            case nameof(ItemModel.IsMtab):
                OnPropertyChanged(nameof(IsMtab));
                break;
            case nameof(ItemModel.IsActionItem):
                OnPropertyChanged(nameof(IsActionItem));
                break;
            case nameof(ItemModel.ActionKind):
                OnPropertyChanged(nameof(ActionKind));
                OnPropertyChanged(nameof(DisplayPath));
                break;
            case nameof(ItemModel.Path):
                OnPropertyChanged(nameof(Path));
                break;
            case nameof(ItemModel.Type):
                OnPropertyChanged(nameof(Type));
                break;
            case nameof(ItemModel.SearchContent):
                OnPropertyChanged(nameof(SearchContent));
                break;
            default:
                RaiseAll();
                break;
        }
    }

    private void RaiseAll()
    {
        OnPropertyChanged(nameof(Item));
        OnPropertyChanged(nameof(IsSeparator));
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(DisplayPath));
        OnPropertyChanged(nameof(Icon));
        OnPropertyChanged(nameof(HealthState));
        OnPropertyChanged(nameof(IsMtab));
        OnPropertyChanged(nameof(IsClipboardText));
        OnPropertyChanged(nameof(IsClipboardImage));
        OnPropertyChanged(nameof(IsActionItem));
        OnPropertyChanged(nameof(ActionKind));
        OnPropertyChanged(nameof(Path));
        OnPropertyChanged(nameof(Type));
        OnPropertyChanged(nameof(IsFavorite));
        OnPropertyChanged(nameof(SearchContent));
    }
}
