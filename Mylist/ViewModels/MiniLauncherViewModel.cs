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
                Rebuild();
            }
        }
    }

    public ICommand ActivateItemCommand { get; }

    public ICommand CloseCommand { get; }

    public ICommand OpenIndexedItemCommand { get; }

    public void Refresh() => Rebuild();

    private void Rebuild()
    {
        var all = _itemsProvider()
            .Where(item => !item.IsClipboardImage)
            .ToList();

        var searching = !string.IsNullOrWhiteSpace(_searchText);
        var matches = (searching
                ? all.Where(item => MatchesSearch(item, _searchText))
                : all)
            .OrderByDescending(item => item.IsFavorite)
            .ThenByDescending(item => item.LastOpenedDate)
            .ToList();

        Items.Clear();
        var listItems = matches.Take(MaxListItems).ToList();
        for (var i = 0; i < listItems.Count; i++)
        {
            Items.Add(new MiniLauncherListItemViewModel(listItems[i], i + 1, ActivateItemCommand));
        }

        // The orbit mirrors the active query: center on the top match, satellites
        // are the remaining matches. With no query it falls back to favorites,
        // then most-recently-opened, centered on the most recent item.
        List<ItemModel> orbitSource;
        if (searching)
        {
            CenterItem = matches.FirstOrDefault();
            orbitSource = matches.Skip(1).Take(MaxOrbitNodes).ToList();
        }
        else
        {
            CenterItem = all
                .OrderByDescending(item => item.LastOpenedDate)
                .FirstOrDefault();

            orbitSource = all
                .Where(item => item.IsFavorite)
                .Take(MaxOrbitNodes)
                .ToList();

            if (orbitSource.Count == 0)
            {
                orbitSource = all
                    .OrderByDescending(item => item.LastOpenedDate)
                    .Take(MaxOrbitNodes)
                    .ToList();
            }
        }

        BuildOrbit(orbitSource);
    }

    private void BuildOrbit(IReadOnlyList<ItemModel> source)
    {
        OrbitNodes.Clear();
        var count = source.Count;
        for (var i = 0; i < count; i++)
        {
            var node = new MiniLauncherOrbitNodeViewModel(source[i], ActivateItemCommand);
            var (x, y) = OrbitLayoutService.ComputePoint(i, Math.Max(count, MaxOrbitNodes), CenterX, CenterY, RadiusX, RadiusY);
            node.LineX2 = x;
            node.LineY2 = y;
            node.NodeLeft = x - NodeSize / 2;
            node.NodeTop = y - NodeSize / 2;
            OrbitNodes.Add(node);
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
