using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using MyList.Helpers;

namespace MyList.ViewModels;

public sealed class CommandPaletteEntry
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public Action Execute { get; init; } = () => { };
}

public sealed class CommandPaletteViewModel : ViewModelBase
{
    private readonly IReadOnlyList<CommandPaletteEntry> _allEntries;
    private string _query = string.Empty;
    private CommandPaletteEntry? _selectedEntry;

    public CommandPaletteViewModel(IReadOnlyList<CommandPaletteEntry> entries)
    {
        _allEntries = entries;
        FilteredEntries = new ObservableCollection<CommandPaletteEntry>();
        ExecuteSelectedCommand = new RelayCommand(ExecuteSelected, () => SelectedEntry is not null);
        Refresh();
    }

    public ObservableCollection<CommandPaletteEntry> FilteredEntries { get; }

    public ICommand ExecuteSelectedCommand { get; }

    public string Query
    {
        get => _query;
        set
        {
            if (SetProperty(ref _query, value))
            {
                Refresh();
            }
        }
    }

    public CommandPaletteEntry? SelectedEntry
    {
        get => _selectedEntry;
        set
        {
            if (SetProperty(ref _selectedEntry, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public void Refresh()
    {
        var query = Query.Trim();
        var results = _allEntries
            .Where(entry => string.IsNullOrWhiteSpace(query) ||
                            entry.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                            entry.Description.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(30)
            .ToList();

        FilteredEntries.Clear();
        foreach (var entry in results)
        {
            FilteredEntries.Add(entry);
        }

        SelectedEntry = FilteredEntries.FirstOrDefault();
    }

    public void ExecuteSelected()
    {
        SelectedEntry?.Execute();
    }
}
