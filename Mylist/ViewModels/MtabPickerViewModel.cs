using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using MyList.Models;

namespace MyList.ViewModels;

public sealed class MtabPickerViewModel : ViewModelBase
{
    private string _searchText = string.Empty;
    private ItemModel? _selectedMtab;

    public MtabPickerViewModel(IEnumerable<ItemModel> mtabs)
    {
        Mtabs = new ObservableCollection<ItemModel>(
            mtabs
                .Where(item => item is not null && item.IsMtab)
                .OrderBy(item => item.Name));

        FilteredMtabs = CollectionViewSource.GetDefaultView(Mtabs);
        FilteredMtabs.Filter = FilterMtab;
        SelectedMtab = Mtabs.FirstOrDefault();
    }

    public ObservableCollection<ItemModel> Mtabs { get; }

    public ICollectionView FilteredMtabs { get; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                FilteredMtabs.Refresh();
            }
        }
    }

    public ItemModel? SelectedMtab
    {
        get => _selectedMtab;
        set => SetProperty(ref _selectedMtab, value);
    }

    public bool HasMtabs => Mtabs.Count > 0;

    private bool FilterMtab(object obj)
    {
        if (obj is not ItemModel mtab)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            return true;
        }

        var term = SearchText.Trim();
        return mtab.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
               || mtab.DisplayPath.Contains(term, StringComparison.OrdinalIgnoreCase)
               || mtab.MtabSearchHint.Contains(term, StringComparison.OrdinalIgnoreCase);
    }
}
