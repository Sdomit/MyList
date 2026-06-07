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
    public const double PanelSize = 280;
    private const double NodeSize = 32;
    private const double CenterX = PanelSize / 2;
    private const double CenterY = PanelSize / 2;
    private const double RadiusX = 100;
    private const double RadiusY = 100;
    private const int MaxNodes = 8;

    private readonly Func<IReadOnlyList<ItemModel>> _itemsProvider;
    private readonly LauncherService _launcherService;
    private readonly Action _closeRequested;
    private ItemModel? _centerItem;

    public MiniLauncherViewModel(Func<IReadOnlyList<ItemModel>> itemsProvider, LauncherService launcherService, Action closeRequested)
    {
        _itemsProvider = itemsProvider;
        _launcherService = launcherService;
        _closeRequested = closeRequested;

        OrbitNodes = new ObservableCollection<MiniLauncherOrbitNodeViewModel>();
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

        Refresh();
    }

    public ObservableCollection<MiniLauncherOrbitNodeViewModel> OrbitNodes { get; }

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

    public ICommand ActivateItemCommand { get; }

    public ICommand CloseCommand { get; }

    public void Refresh()
    {
        var items = _itemsProvider().ToList();
        var favorites = items
            .Where(item => item.IsFavorite && !item.IsClipboardImage)
            .Take(MaxNodes)
            .ToList();

        if (favorites.Count == 0)
        {
            favorites = items
                .Where(item => !item.IsClipboardImage)
                .OrderByDescending(item => item.LastOpenedDate)
                .Take(MaxNodes)
                .ToList();
        }

        CenterItem = items
            .Where(item => !item.IsClipboardImage)
            .OrderByDescending(item => item.LastOpenedDate)
            .FirstOrDefault();

        OrbitNodes.Clear();
        var count = favorites.Count;
        for (var i = 0; i < count; i++)
        {
            var node = new MiniLauncherOrbitNodeViewModel(favorites[i], ActivateItemCommand);
            var (x, y) = OrbitLayoutService.ComputePoint(i, Math.Max(count, 5), CenterX, CenterY, RadiusX, RadiusY);
            node.LineX2 = x;
            node.LineY2 = y;
            node.NodeLeft = x - NodeSize / 2;
            node.NodeTop = y - NodeSize / 2;
            OrbitNodes.Add(node);
        }
    }
}
