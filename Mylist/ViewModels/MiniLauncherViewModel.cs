using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using MyList.Helpers;
using MyList.Models;
using MyList.Services;

namespace MyList.ViewModels;

public sealed class MiniLauncherViewModel : ViewModelBase
{
    public const double PanelSize = 240;
    private const double NodeSize = 32;
    private const double CenterX = PanelSize / 2;
    private const double CenterY = PanelSize / 2;
    private const double RadiusX = 90;
    private const double RadiusY = 78;
    private const int MaxOrbitNodes = 6;
    private const int MaxListItems = 9;

    private readonly Func<IReadOnlyList<ItemModel>> _itemsProvider;
    private readonly LauncherService _launcherService;
    private readonly Action _closeRequested;
    private ItemModel? _centerItem;
    private string _searchText = string.Empty;

    public MiniLauncherViewModel(Func<IReadOnlyList<ItemModel>> itemsProvider, LauncherService launcherService, Action closeRequested)
    {
        _itemsProvider = itemsProvider;
        _launcherService = launcherService;
        _closeRequested = closeRequested;

        OrbitNodes = new ObservableCollection<MiniLauncherOrbitNodeViewModel>();
        Items = new ObservableCollection<MiniLauncherListItemViewModel>();

        ActivateItemCommand = new RelayCommand<ItemModel>(item =>
        {
            if (item is null)
            {
                return;
            }

            _launcherService.Open(item);
            _closeRequested();
        });
        CloseCommand = new RelayCommand(() => _closeRequested());
        OpenIndexedItemCommand = new RelayCommand<string>(p =>
        {
            if (!int.TryParse(p, out var index) || index < 1)
            {
                return;
            }

            var slot = Items.FirstOrDefault(item => item.Index == index);
            if (slot?.Item is null)
            {
                return;
            }

            _launcherService.Open(slot.Item);
            _closeRequested();
        });

        Refresh();
    }

    public ObservableCollection<MiniLauncherOrbitNodeViewModel> OrbitNodes { get; }

    public ObservableCollection<MiniLauncherListItemViewModel> Items { get; }

    public ItemModel? CenterItem
    {
        get => _centerItem;
        private set
        {
            if (SetProperty(ref _centerItem, value))
            {
                OnPropertyChanged(nameof(CenterLabel));
                OnPropertyChanged(nameof(HasCenterItem));
            }
        }
    }

    public string CenterLabel => CenterItem?.Name ?? "MyList";

    public bool HasCenterItem => CenterItem is not null;

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value ?? string.Empty))
            {
                RebuildList();
            }
        }
    }

    public ICommand ActivateItemCommand { get; }

    public ICommand CloseCommand { get; }

    public ICommand OpenIndexedItemCommand { get; }

    public void Refresh()
    {
        var all = _itemsProvider()
            .Where(item => !item.IsClipboardImage)
            .ToList();

        CenterItem = all
            .OrderByDescending(item => item.LastOpenedDate)
            .FirstOrDefault();

        var favorites = all
            .Where(item => item.IsFavorite)
            .Take(MaxOrbitNodes)
            .ToList();

        if (favorites.Count == 0)
        {
            favorites = all
                .OrderByDescending(item => item.LastOpenedDate)
                .Take(MaxOrbitNodes)
                .ToList();
        }

        OrbitNodes.Clear();
        var count = favorites.Count;
        for (var i = 0; i < count; i++)
        {
            var node = new MiniLauncherOrbitNodeViewModel(favorites[i], ActivateItemCommand);
            var (x, y) = OrbitLayoutService.ComputePoint(i, Math.Max(count, MaxOrbitNodes), CenterX, CenterY, RadiusX, RadiusY);
            node.LineX2 = x;
            node.LineY2 = y;
            node.NodeLeft = x - NodeSize / 2;
            node.NodeTop = y - NodeSize / 2;
            OrbitNodes.Add(node);
        }

        RebuildList();
    }

    private void RebuildList()
    {
        Items.Clear();
        var all = _itemsProvider()
            .Where(item => !item.IsClipboardImage);

        var filtered = string.IsNullOrWhiteSpace(_searchText)
            ? all
            : all.Where(item => MatchesSearch(item, _searchText));

        var ordered = filtered
            .OrderByDescending(item => item.IsFavorite)
            .ThenByDescending(item => item.LastOpenedDate)
            .Take(MaxListItems)
            .ToList();

        for (var i = 0; i < ordered.Count; i++)
        {
            Items.Add(new MiniLauncherListItemViewModel(ordered[i], i + 1, ActivateItemCommand));
        }
    }

    private static bool MatchesSearch(ItemModel item, string text)
    {
        var needle = text.Trim();
        if (needle.Length == 0)
        {
            return true;
        }

        return (item.Name?.Contains(needle, StringComparison.OrdinalIgnoreCase) ?? false)
               || (item.DisplayPath?.Contains(needle, StringComparison.OrdinalIgnoreCase) ?? false);
    }
}
