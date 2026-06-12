using System.Windows.Input;
using MahApps.Metro.IconPacks;
using MyList.Helpers;
using MyList.Models;
using MyList.Services;

namespace MyList.ViewModels;

/// <summary>
/// One node on an orbit ring. Wraps an <see cref="OrbitCollection"/> (ring 1),
/// an <see cref="ItemModel"/> (ring 2), or the "More…" sentinel. Position is
/// assigned after construction by <see cref="MiniLauncherViewModel"/>.
/// </summary>
public sealed class OrbitSlotViewModel : ObservableObject
{
    private bool _isSelected;
    private double _centreX;
    private double _centreY;
    private double _discLeft;
    private double _discTop;
    private double _labelLeft;
    private double _labelTop;
    private int _index;

    private OrbitSlotViewModel()
    {
    }

    public OrbitCollection? Collection { get; private init; }

    public ItemModel? Item { get; private init; }

    public bool IsMore { get; private init; }

    public bool IsCollection => Collection is not null;

    public bool IsItem => Item is not null;

    public string Name { get; private init; } = string.Empty;

    public string Label { get; private init; } = string.Empty;

    public PackIconLucideKind IconKind { get; private init; } = PackIconLucideKind.Folder;

    /// <summary>Drives the disc border colour through <see cref="HealthStateToBrushConverter"/>.</summary>
    public ItemHealthState HealthState { get; private init; } = ItemHealthState.Healthy;

    public int Index
    {
        get => _index;
        private set
        {
            if (SetProperty(ref _index, value))
            {
                OnPropertyChanged(nameof(IndexLabel));
            }
        }
    }

    public string IndexLabel => Index.ToString();

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public double CentreX
    {
        get => _centreX;
        private set => SetProperty(ref _centreX, value);
    }

    public double CentreY
    {
        get => _centreY;
        private set => SetProperty(ref _centreY, value);
    }

    public double DiscLeft
    {
        get => _discLeft;
        private set => SetProperty(ref _discLeft, value);
    }

    public double DiscTop
    {
        get => _discTop;
        private set => SetProperty(ref _discTop, value);
    }

    public double LabelLeft
    {
        get => _labelLeft;
        private set => SetProperty(ref _labelLeft, value);
    }

    public double LabelTop
    {
        get => _labelTop;
        private set => SetProperty(ref _labelTop, value);
    }

    public ICommand? ClickCommand { get; set; }

    public ICommand? DeleteCommand { get; set; }

    // Items and user collections can be removed; smart views (Recent, Favorites)
    // and the "More…" sentinel cannot.
    public bool CanDelete => IsItem || (IsCollection && Collection is { IsSmart: false });

    public void SetPosition(double centreX, double centreY, double nodeRadius, int zeroBasedIndex)
    {
        CentreX = centreX;
        CentreY = centreY;
        DiscLeft = centreX - nodeRadius;
        DiscTop = centreY - nodeRadius;
        LabelLeft = centreX - 36;
        LabelTop = centreY + nodeRadius + 5;
        Index = zeroBasedIndex + 1;
    }

    public static OrbitSlotViewModel FromCollection(OrbitCollection collection) => new()
    {
        Collection = collection,
        Name = collection.Name,
        Label = Truncate(collection.Name),
        IconKind = PackIconLucideKind.Folder,
        HealthState = ItemHealthState.Healthy,
    };

    public static OrbitSlotViewModel FromItem(ItemModel item) => new()
    {
        Item = item,
        Name = item.Name,
        Label = Truncate(item.Name),
        IconKind = IconForKind(item.Kind),
        HealthState = item.HealthState,
    };

    public static OrbitSlotViewModel MoreSlot() => new()
    {
        IsMore = true,
        Name = "More…",
        Label = "More…",
        IconKind = PackIconLucideKind.LayoutGrid,
        HealthState = ItemHealthState.Healthy,
    };

    private static string Truncate(string value) =>
        value.Length > 11 ? value[..10] + "…" : value;

    private static PackIconLucideKind IconForKind(ContentKind kind) => kind switch
    {
        ContentKind.File => PackIconLucideKind.File,
        ContentKind.Folder => PackIconLucideKind.Folder,
        ContentKind.Mtab => PackIconLucideKind.Link,
        ContentKind.Clip => PackIconLucideKind.ClipboardCheck,
        ContentKind.Action => PackIconLucideKind.Zap,
        _ => PackIconLucideKind.File,
    };
}
