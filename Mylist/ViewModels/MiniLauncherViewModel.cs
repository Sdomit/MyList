using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using MyList.Helpers;
using MyList.Models;
using MyList.Services;

namespace MyList.ViewModels;

/// <summary>
/// Two-level orbit quick-menu view-model.
///
/// State machine:
///   Root ring (collections)  ──DrillInto──▶  Collection ring (items)
///                            ◀──DrillUp────
///
/// Search is orthogonal: any text in <see cref="SearchText"/> swaps the body to a
/// flat result list across all items; clearing it restores the current ring.
/// </summary>
public sealed class MiniLauncherViewModel : ViewModelBase
{
    private const double Cx = 200;
    private const double Cy = 168;
    private const double R = 116;
    private const double Nr = 24;
    private const double GhostR = 146;
    private const int MaxResults = 7;

    private readonly IOrbitSourceService _source;
    private readonly LauncherService _launcherService;
    private readonly Action _closeRequested;
    private readonly Action _moreRequested;
    private readonly Action<ItemModel> _deleteItem;
    private readonly Action<Guid> _deleteCollection;
    private readonly ICommand _deleteResultCommand;

    private OrbitCollection? _currentCollection;
    private int _selectedIndex = -1;
    private string _searchText = string.Empty;
    private string _statusText = string.Empty;

    public MiniLauncherViewModel(
        IOrbitSourceService source,
        LauncherService launcherService,
        Action closeRequested,
        Action moreRequested,
        Action<ItemModel> deleteItem,
        Action<Guid> deleteCollection)
    {
        _source = source;
        _launcherService = launcherService;
        _closeRequested = closeRequested;
        _moreRequested = moreRequested;
        _deleteItem = deleteItem;
        _deleteCollection = deleteCollection;

        VisibleSlots = new ObservableCollection<OrbitSlotViewModel>();
        SearchResults = new ObservableCollection<MiniLauncherListItemViewModel>();

        DrillUpCommand = new RelayCommand(DrillUp, () => IsInCollection);
        CloseCommand = new RelayCommand(() => _closeRequested());
        MoreCommand = new RelayCommand(RequestMore);
        ActivateSelectedCommand = new RelayCommand(ActivateSelected);
        RotateCommand = new RelayCommand<string>(p => Rotate(p == "-1" ? -1 : 1));
        OpenIndexedCommand = new RelayCommand<string>(OpenIndexed);
        ActivateResultCommand = new RelayCommand<ItemModel>(item =>
        {
            if (item is not null)
            {
                Launch(item);
            }
        });
        _deleteResultCommand = new RelayCommand<ItemModel>(item =>
        {
            if (item is not null)
            {
                DeleteResult(item);
            }
        });

        Repopulate();
    }

    // ── Slots & results ──────────────────────────────────────────────────────
    public ObservableCollection<OrbitSlotViewModel> VisibleSlots { get; }

    public ObservableCollection<MiniLauncherListItemViewModel> SearchResults { get; }

    public bool HasResults => SearchResults.Count > 0;

    // ── Two-level state ──────────────────────────────────────────────────────
    public bool IsInCollection => _currentCollection is not null;

    public string CurrentCollectionName => _currentCollection?.Name ?? string.Empty;

    public string HubLabel => IsInCollection ? CollectionInitials(_currentCollection!.Name) : "ML";

    public string HubSublabel => IsInCollection ? "back" : "MyList";

    public string EyebrowText => IsInCollection
        ? _currentCollection!.Name.ToUpperInvariant()
        : "COLLECTIONS";

    public bool FooterBackVisible => IsInCollection;

    // ── Selection ────────────────────────────────────────────────────────────
    public int SelectedIndex
    {
        get => _selectedIndex;
        private set
        {
            if (_selectedIndex == value)
            {
                return;
            }

            if (_selectedIndex >= 0 && _selectedIndex < VisibleSlots.Count)
            {
                VisibleSlots[_selectedIndex].IsSelected = false;
            }

            _selectedIndex = value;

            if (_selectedIndex >= 0 && _selectedIndex < VisibleSlots.Count)
            {
                VisibleSlots[_selectedIndex].IsSelected = true;
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(SelectedX));
            OnPropertyChanged(nameof(SelectedY));
        }
    }

    public bool HasSelection => _selectedIndex >= 0 && _selectedIndex < VisibleSlots.Count;

    public double SelectedX => HasSelection ? VisibleSlots[_selectedIndex].CentreX : Cx;

    public double SelectedY => HasSelection ? VisibleSlots[_selectedIndex].CentreY : Cy;

    // ── Search ───────────────────────────────────────────────────────────────
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value ?? string.Empty))
            {
                StatusText = string.Empty;
                SelectedIndex = -1;
                RefreshSearch();
                OnPropertyChanged(nameof(IsSearching));
            }
        }
    }

    public bool IsSearching => !string.IsNullOrWhiteSpace(_searchText);

    public string StatusText
    {
        get => _statusText;
        private set
        {
            if (SetProperty(ref _statusText, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(HasStatus));
            }
        }
    }

    public bool HasStatus => !string.IsNullOrEmpty(_statusText);

    // ── Geometry (bound by the canvas decoration layer) ──────────────────────
    public double GhostRingLeft => Cx - GhostR;
    public double GhostRingTop => Cy - GhostR;
    public double GhostRingSize => GhostR * 2;
    public double GuideRingLeft => Cx - R;
    public double GuideRingTop => Cy - R;
    public double GuideRingSize => R * 2;
    public double HubLeft => Cx - 36;
    public double HubTop => Cy - 36;
    public double CentreX => Cx;
    public double CentreY => Cy;

    // ── Commands ─────────────────────────────────────────────────────────────
    public ICommand DrillUpCommand { get; }
    public ICommand CloseCommand { get; }
    public ICommand MoreCommand { get; }
    public ICommand ActivateSelectedCommand { get; }
    public ICommand RotateCommand { get; }
    public ICommand OpenIndexedCommand { get; }
    public ICommand ActivateResultCommand { get; }

    // ── Lifecycle ────────────────────────────────────────────────────────────
    public void Refresh()
    {
        _currentCollection = null;
        _searchText = string.Empty;
        OnPropertyChanged(nameof(SearchText));
        OnPropertyChanged(nameof(IsSearching));
        StatusText = string.Empty;
        SearchResults.Clear();
        OnPropertyChanged(nameof(HasResults));
        NotifyLevelChanged();
        Repopulate();
    }

    // ── Navigation ───────────────────────────────────────────────────────────
    private void DrillInto(OrbitCollection collection)
    {
        _currentCollection = collection;
        NotifyLevelChanged();
        Repopulate();
    }

    private void DrillUp()
    {
        if (!IsInCollection)
        {
            return;
        }

        _currentCollection = null;
        NotifyLevelChanged();
        Repopulate();
    }

    private void NotifyLevelChanged()
    {
        OnPropertyChanged(nameof(IsInCollection));
        OnPropertyChanged(nameof(CurrentCollectionName));
        OnPropertyChanged(nameof(HubLabel));
        OnPropertyChanged(nameof(HubSublabel));
        OnPropertyChanged(nameof(EyebrowText));
        OnPropertyChanged(nameof(FooterBackVisible));
        CommandManager.InvalidateRequerySuggested();
    }

    // ── Slot population ──────────────────────────────────────────────────────
    private void Repopulate()
    {
        SelectedIndex = -1;
        VisibleSlots.Clear();

        var nodes = IsInCollection
            ? _source.GetItems(_currentCollection!).Select(OrbitSlotViewModel.FromItem).ToList()
            : _source.GetRootCollections().Select(OrbitSlotViewModel.FromCollection).ToList();

        var capped = nodes.Count >= 5
            ? nodes.Take(5).Append(OrbitSlotViewModel.MoreSlot()).ToList()
            : nodes.Append(OrbitSlotViewModel.MoreSlot()).ToList();

        var positions = OrbitLayoutService.Compute(capped.Count, Cx, Cy, R, R);
        for (var i = 0; i < capped.Count; i++)
        {
            var slot = capped[i];
            slot.SetPosition(positions[i].X, positions[i].Y, Nr, i);
            slot.ClickCommand = new RelayCommand(() => Activate(slot));
            slot.DeleteCommand = new RelayCommand(() => DeleteSlot(slot), () => slot.CanDelete);
            VisibleSlots.Add(slot);
        }
    }

    private void DeleteSlot(OrbitSlotViewModel slot)
    {
        if (slot.IsItem && slot.Item is not null)
        {
            _deleteItem(slot.Item);
        }
        else if (slot.IsCollection && slot.Collection is { IsSmart: false } collection
                 && Guid.TryParse(collection.Id, out var id))
        {
            _deleteCollection(id);
        }
        else
        {
            return;
        }

        Repopulate();
    }

    private void DeleteResult(ItemModel item)
    {
        _deleteItem(item);
        RefreshSearch();
    }

    private void Activate(OrbitSlotViewModel slot)
    {
        if (slot.IsMore)
        {
            RequestMore();
        }
        else if (slot.Collection is not null)
        {
            DrillInto(slot.Collection);
        }
        else if (slot.Item is not null)
        {
            Launch(slot.Item);
        }
    }

    private void ActivateSelected()
    {
        if (HasSelection)
        {
            Activate(VisibleSlots[_selectedIndex]);
            return;
        }

        if (IsSearching)
        {
            var first = SearchResults.FirstOrDefault();
            if (first?.Item is not null)
            {
                Launch(first.Item);
            }
        }
    }

    private void Rotate(int delta)
    {
        if (IsSearching || VisibleSlots.Count == 0)
        {
            return;
        }

        var n = VisibleSlots.Count;
        SelectedIndex = _selectedIndex < 0
            ? (delta > 0 ? 0 : n - 1)
            : (((_selectedIndex + delta) % n) + n) % n;
    }

    private void OpenIndexed(string? parameter)
    {
        if (IsSearching || !int.TryParse(parameter, out var index))
        {
            return;
        }

        var slot = VisibleSlots.FirstOrDefault(s => s.Index == index);
        if (slot is not null)
        {
            Activate(slot);
        }
    }

    private void RequestMore()
    {
        _moreRequested();
        _closeRequested();
    }

    // ── Launch ───────────────────────────────────────────────────────────────
    private void Launch(ItemModel item)
    {
        if (item.HealthState is ItemHealthState.Missing or ItemHealthState.Offline)
        {
            StatusText = item.HealthState == ItemHealthState.Offline
                ? $"Offline: {item.Path}"
                : $"Path not found: {item.Path}";
            return;
        }

        try
        {
            _launcherService.Open(item);
            _closeRequested();
        }
        catch (Exception ex)
        {
            StatusText = $"Couldn't open '{item.Name}': {ex.Message}";
        }
    }

    // ── Search ───────────────────────────────────────────────────────────────
    private void RefreshSearch()
    {
        SearchResults.Clear();

        if (!string.IsNullOrWhiteSpace(_searchText))
        {
            var needle = _searchText.Trim();
            var matches = _source.GetAllItems()
                .Where(item => Matches(item, needle))
                .OrderByDescending(item => item.IsFavorite)
                .ThenByDescending(item => item.LastOpenedDate)
                .Take(MaxResults)
                .ToList();

            for (var i = 0; i < matches.Count; i++)
            {
                SearchResults.Add(new MiniLauncherListItemViewModel(
                    matches[i], i + 1, ActivateResultCommand, _deleteResultCommand));
            }
        }

        OnPropertyChanged(nameof(HasResults));
    }

    private static bool Matches(ItemModel item, string needle) =>
        (item.Name?.Contains(needle, StringComparison.OrdinalIgnoreCase) ?? false)
        || (item.DisplayPath?.Contains(needle, StringComparison.OrdinalIgnoreCase) ?? false);

    private static string CollectionInitials(string name)
    {
        var words = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return "?";
        }

        return words.Length > 1
            ? string.Concat(words.Take(2).Select(w => char.ToUpperInvariant(w[0])))
            : char.ToUpperInvariant(words[0][0]).ToString();
    }
}
