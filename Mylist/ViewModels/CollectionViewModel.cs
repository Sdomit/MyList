using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using MyList.Helpers;
using MyList.Models;

namespace MyList.ViewModels;

public sealed class CollectionViewModel : ViewModelBase
{
    private readonly CollectionModel _model;
    private readonly ObservableCollection<ItemModel> _items;
    private readonly ObservableCollection<CollectionEntryViewModel> _entries;
    private SearchQuery _searchQuery = new();
    private bool _isExpanded;
    private bool _isTabDragTarget;
    private bool _isTabDropBlocked;
    private bool _isTabInsertionBefore;
    private bool _isTabInsertionAfter;

    public CollectionViewModel(CollectionModel model, ObservableCollection<CollectionEntryViewModel> entries)
    {
        _model = model;
        _entries = entries;
        _items = new ObservableCollection<ItemModel>(_entries.Where(entry => entry.Item is not null).Select(entry => entry.Item!));
        FilteredEntries = CollectionViewSource.GetDefaultView(_entries);
        FilteredEntries.Filter = FilterEntry;
    }

    public Guid Id => _model.Id;

    public string Name
    {
        get => _model.Name;
        set
        {
            if (_model.Name != value)
            {
                _model.Name = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsSmart
    {
        get => _model.IsSmart;
        set
        {
            if (_model.IsSmart != value)
            {
                _model.IsSmart = value;
                OnPropertyChanged();
            }
        }
    }

    public SmartCollectionType SmartType
    {
        get => _model.SmartType;
        set
        {
            if (_model.SmartType != value)
            {
                _model.SmartType = value;
                OnPropertyChanged();
            }
        }
    }

    public string SmartValue
    {
        get => _model.SmartValue;
        set
        {
            if (_model.SmartValue != value)
            {
                _model.SmartValue = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    public ObservableCollection<ItemModel> Items => _items;

    public ObservableCollection<CollectionEntryViewModel> Entries => _entries;

    public ICollectionView FilteredEntries { get; }

    public bool IsTabDragTarget
    {
        get => _isTabDragTarget;
        set => SetProperty(ref _isTabDragTarget, value);
    }

    public bool IsTabDropBlocked
    {
        get => _isTabDropBlocked;
        set => SetProperty(ref _isTabDropBlocked, value);
    }

    public bool IsTabInsertionBefore
    {
        get => _isTabInsertionBefore;
        set => SetProperty(ref _isTabInsertionBefore, value);
    }

    public bool IsTabInsertionAfter
    {
        get => _isTabInsertionAfter;
        set => SetProperty(ref _isTabInsertionAfter, value);
    }

    public void UpdateSearchQuery(SearchQuery searchQuery)
    {
        _searchQuery = searchQuery ?? new SearchQuery();
        FilteredEntries.Refresh();
    }

    public void RefreshSmartItems(ObservableCollection<ItemModel> allItems)
    {
        if (!IsSmart || SmartType == SmartCollectionType.None)
        {
            return;
        }

        ResetEntries(BuildSmartEntries(allItems));
    }

    public void ResetEntries(IEnumerable<CollectionEntryViewModel> entries)
    {
        _entries.Clear();
        _items.Clear();
        foreach (var entry in entries)
        {
            _entries.Add(entry);
            if (entry.Item is not null)
            {
                _items.Add(entry.Item);
            }
        }
        FilteredEntries.Refresh();
    }

    public CollectionModel Model => _model;

    private bool FilterEntry(object obj)
    {
        if (obj is not CollectionEntryViewModel entry)
        {
            return false;
        }

        if (entry.IsSeparator)
        {
            return _searchQuery.IsEmpty && !IsSmart;
        }

        if (entry.Item is not ItemModel item)
        {
            return false;
        }

        if (_searchQuery.IsEmpty)
        {
            return true;
        }

        if (_searchQuery.ItemType is not null && item.Type != _searchQuery.ItemType)
        {
            return false;
        }

        if (_searchQuery.IsOffline is not null && item.IsOffline != _searchQuery.IsOffline.Value)
        {
            return false;
        }

        if (_searchQuery.IsFavorite is not null && item.IsFavorite != _searchQuery.IsFavorite.Value)
        {
            return false;
        }

        foreach (var tagQuery in _searchQuery.Tags)
        {
            if (!item.Tags.Any(tag => tag.Contains(tagQuery, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }
        }

        foreach (var pathTerm in _searchQuery.PathTerms)
        {
            var source = item.IsMtab ? item.MtabSearchHint : item.Path;
            if (string.IsNullOrWhiteSpace(source) || !source.Contains(pathTerm, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        foreach (var term in _searchQuery.FreeTerms)
        {
            var matched = item.SearchContent.Contains(term, StringComparison.OrdinalIgnoreCase);
            if (!matched)
            {
                return false;
            }
        }

        return true;
    }

    private IEnumerable<CollectionEntryViewModel> BuildSmartEntries(ObservableCollection<ItemModel> allItems)
    {
        IEnumerable<ItemModel> source;

        if (SmartType == SmartCollectionType.Tag && !string.IsNullOrWhiteSpace(SmartValue))
        {
            source = allItems.Where(item => item.Tags.Any(tag => tag.Equals(SmartValue, StringComparison.OrdinalIgnoreCase)));
        }
        else if (SmartType == SmartCollectionType.Recent)
        {
            var days = 7;
            if (int.TryParse(SmartValue, out var parsed))
            {
                days = parsed;
            }

            var cutoff = DateTime.UtcNow.AddDays(-days);
            source = allItems
                .Where(item => item.LastOpenedDate >= cutoff)
                .OrderByDescending(item => item.LastOpenedDate);
        }
        else
        {
            source = allItems;
        }

        return source.Select((item, i) => new CollectionEntryViewModel(
            new CollectionEntryModel { Kind = CollectionEntryKind.Item, ItemId = item.Id },
            item)
        {
            ShortcutIndex = i + 1
        });
    }

    public override string ToString()
    {
        return Name;
    }
}
