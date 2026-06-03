using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using MyList.Helpers;
using MyList.Models;
using MyList.Services;

namespace MyList.ViewModels;

public sealed class DuplicateManagerViewModel : ViewModelBase
{
    private readonly DuplicateDetectionService _duplicateService = new();
    private readonly MainViewModel _mainViewModel;
    private DuplicateGroup? _selectedGroup;

    public DuplicateManagerViewModel(MainViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;
        Groups = new ObservableCollection<DuplicateGroup>();
        RefreshCommand = new RelayCommand(Refresh);
        MergeSelectedGroupCommand = new RelayCommand(MergeSelectedGroup, () => SelectedGroup is not null);
        MergeAllCommand = new RelayCommand(MergeAll, () => Groups.Count > 0);
        Refresh();
    }

    public ObservableCollection<DuplicateGroup> Groups { get; }

    public ICommand RefreshCommand { get; }

    public ICommand MergeSelectedGroupCommand { get; }

    public ICommand MergeAllCommand { get; }

    public DuplicateGroup? SelectedGroup
    {
        get => _selectedGroup;
        set
        {
            if (SetProperty(ref _selectedGroup, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public void Refresh()
    {
        Groups.Clear();
        foreach (var group in _duplicateService.BuildGroups(_mainViewModel.AllItems))
        {
            Groups.Add(group);
        }

        SelectedGroup = Groups.FirstOrDefault();
    }

    private void MergeSelectedGroup()
    {
        if (SelectedGroup is null)
        {
            return;
        }

        _mainViewModel.MergeDuplicateGroup(SelectedGroup);
        Refresh();
    }

    private void MergeAll()
    {
        foreach (var group in Groups.ToList())
        {
            _mainViewModel.MergeDuplicateGroup(group);
        }

        Refresh();
    }
}
