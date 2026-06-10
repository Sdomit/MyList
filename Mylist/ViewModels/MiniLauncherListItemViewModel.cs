using System.Windows.Input;
using MyList.Helpers;
using MyList.Models;

namespace MyList.ViewModels;

public sealed class MiniLauncherListItemViewModel : ViewModelBase
{
    private int _index;

    public MiniLauncherListItemViewModel(ItemModel item, int index, ICommand activateCommand)
    {
        Item = item;
        _index = index;
        ActivateCommand = new RelayCommand(() => activateCommand.Execute(item));
    }

    public ItemModel Item { get; }

    public int Index
    {
        get => _index;
        set
        {
            if (SetProperty(ref _index, value))
            {
                OnPropertyChanged(nameof(IndexLabel));
                OnPropertyChanged(nameof(HasIndex));
            }
        }
    }

    public bool HasIndex => _index >= 1 && _index <= 9;

    public string IndexLabel => HasIndex ? $"Ctrl+{_index}" : string.Empty;

    public string Name => Item.Name;

    public string DisplayPath => Item.DisplayPath;

    public ContentKind Kind => Item.Kind;

    public ICommand ActivateCommand { get; }
}
