using System.Windows.Input;
using MyList.Helpers;
using MyList.Models;

namespace MyList.ViewModels;

public sealed class MiniLauncherOrbitNodeViewModel : ViewModelBase
{
    private double _nodeLeft;
    private double _nodeTop;
    private double _lineX2;
    private double _lineY2;

    public MiniLauncherOrbitNodeViewModel(ItemModel item, ICommand activateCommand)
    {
        Item = item;
        ActivateCommand = new RelayCommand(() => activateCommand.Execute(item));
    }

    public ItemModel Item { get; }

    public string Label => Item.Name;

    public ICommand ActivateCommand { get; }

    public double NodeLeft
    {
        get => _nodeLeft;
        set => SetProperty(ref _nodeLeft, value);
    }

    public double NodeTop
    {
        get => _nodeTop;
        set => SetProperty(ref _nodeTop, value);
    }

    public double LineX2
    {
        get => _lineX2;
        set => SetProperty(ref _lineX2, value);
    }

    public double LineY2
    {
        get => _lineY2;
        set => SetProperty(ref _lineY2, value);
    }
}
