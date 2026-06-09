using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Threading;
using MyList.Helpers;
using MyList.Models;

namespace MyList.ViewModels;

public sealed class CommandPaletteViewModel : ViewModelBase
{
    private const int ItemsCap = 12;
    private const int CommandsCap = 8;
    private const int SettingsCap = 8;

    private readonly MainViewModel _mainVm;
    private readonly IReadOnlyList<CommandRow> _allCommands;
    private readonly IReadOnlyList<SettingsRow> _allSettings;
    private readonly DispatcherTimer _debounceTimer;

    private string _query = string.Empty;
    private string _pendingQuery = string.Empty;
    private IReadOnlyList<IPaletteRow> _allVisibleRows = Array.Empty<IPaletteRow>();
    private IPaletteRow? _focusedRow;

    public CommandPaletteViewModel(
        MainViewModel mainVm,
        IReadOnlyList<CommandRow> commands,
        IReadOnlyList<SettingsRow> settings)
    {
        _mainVm = mainVm;
        _allCommands = commands;
        _allSettings = settings;

        Items = new PaletteSection("ITEMS");
        Commands = new PaletteSection("COMMANDS");
        Settings = new PaletteSection("SETTINGS");

        _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(60) };
        _debounceTimer.Tick += (_, _) =>
        {
            _debounceTimer.Stop();
            ApplyQuery(_pendingQuery);
        };

        ApplyQuery(string.Empty);
    }

    public PaletteSection Items { get; }
    public PaletteSection Commands { get; }
    public PaletteSection Settings { get; }

    public string Query
    {
        get => _query;
        set
        {
            if (SetProperty(ref _query, value))
            {
                OnPropertyChanged(nameof(ActiveOperators));
                _pendingQuery = value ?? string.Empty;
                _debounceTimer.Stop();
                _debounceTimer.Start();
            }
        }
    }

    public IReadOnlyList<string> ActiveOperators
    {
        get
        {
            var chips = new List<string>();
            foreach (var token in (_query ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var lower = token.ToLowerInvariant();
                if (lower.StartsWith("type:", StringComparison.Ordinal) ||
                    lower.StartsWith("health:", StringComparison.Ordinal) ||
                    lower.StartsWith("tag:", StringComparison.Ordinal))
                {
                    chips.Add(token);
                }
            }

            return chips;
        }
    }

    public IReadOnlyList<IPaletteRow> AllVisibleRows => _allVisibleRows;

    public IPaletteRow? FocusedRow
    {
        get => _focusedRow;
        private set => SetProperty(ref _focusedRow, value);
    }

    public bool HasAnyResults => _allVisibleRows.Count > 0;

    public void MoveFocus(int delta)
    {
        var focusable = _allVisibleRows.Where(r => !r.IsOverflow).ToList();
        if (focusable.Count == 0)
        {
            FocusedRow = null;
            return;
        }

        var currentIndex = _focusedRow is null ? -1 : focusable.IndexOf(_focusedRow);
        if (currentIndex < 0)
        {
            FocusedRow = focusable[0];
            return;
        }

        var next = (currentIndex + delta) % focusable.Count;
        if (next < 0) next += focusable.Count;
        FocusedRow = focusable[next];
    }

    public bool ExecuteFocused()
    {
        var row = _focusedRow;
        if (row is null || row.IsOverflow)
        {
            return false;
        }

        row.Execute();
        return !row.KeepPaletteOpen;
    }

    private void ApplyQuery(string query)
    {
        var trimmed = (query ?? string.Empty).Trim();
        var parsed = SearchQueryParser.Parse(trimmed);

        var items = BuildItemRows(trimmed, parsed);
        var commands = BuildFiltered(_allCommands, trimmed, CommandsCap);
        var settings = BuildFiltered(_allSettings, trimmed, SettingsCap);

        Items.Replace(items);
        Commands.Replace(commands);
        Settings.Replace(settings);

        var flat = new List<IPaletteRow>(items.Count + commands.Count + settings.Count);
        if (Items.HasResults) flat.AddRange(Items.Rows);
        if (Commands.HasResults) flat.AddRange(Commands.Rows);
        if (Settings.HasResults) flat.AddRange(Settings.Rows);
        _allVisibleRows = flat;
        OnPropertyChanged(nameof(AllVisibleRows));
        OnPropertyChanged(nameof(HasAnyResults));

        FocusedRow = flat.FirstOrDefault(r => !r.IsOverflow);
    }

    private IList<IPaletteRow> BuildItemRows(string rawQuery, SearchQuery parsed)
    {
        IEnumerable<ItemModel> source = _mainVm.AllItems;

        if (string.IsNullOrWhiteSpace(rawQuery))
        {
            source = source
                .Where(i => i.LastOpenedDate != default)
                .OrderByDescending(i => i.LastOpenedDate);
        }
        else
        {
            source = source.Where(i => MatchesItem(i, parsed));
        }

        var matched = source.ToList();
        var capped = matched.Take(ItemsCap).ToList();
        var rows = new List<IPaletteRow>(capped.Count + 1);
        foreach (var item in capped)
        {
            rows.Add(new ItemRow(item, m => _mainVm.OpenItemCommand.Execute(m)));
        }

        if (matched.Count > ItemsCap)
        {
            rows.Add(new OverflowRow { Title = $"+{matched.Count - ItemsCap} more — refine search" });
        }

        return rows;
    }

    private static IList<IPaletteRow> BuildFiltered<TRow>(IReadOnlyList<TRow> all, string rawQuery, int cap)
        where TRow : IPaletteRow
    {
        IEnumerable<TRow> source = all;
        if (!string.IsNullOrWhiteSpace(rawQuery))
        {
            var tokens = rawQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            source = source.Where(r => tokens.All(t =>
                (r.Title?.Contains(t, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (r.Subtitle?.Contains(t, StringComparison.OrdinalIgnoreCase) ?? false)));
        }

        var matched = source.ToList();
        var capped = matched.Take(cap).ToList();
        var rows = new List<IPaletteRow>(capped.Count + 1);
        foreach (var r in capped)
        {
            rows.Add(r);
        }

        if (matched.Count > cap)
        {
            rows.Add(new OverflowRow { Title = $"+{matched.Count - cap} more — refine search" });
        }

        return rows;
    }

    private static bool MatchesItem(ItemModel item, SearchQuery parsed)
    {
        if (parsed.ItemType is { } type && item.Type != type)
        {
            return false;
        }

        if (parsed.IsFavorite is { } fav && item.IsFavorite != fav)
        {
            return false;
        }

        if (parsed.IsOffline is { } offline && item.IsOffline != offline)
        {
            return false;
        }

        foreach (var tag in parsed.Tags)
        {
            if (!item.Tags.Any(t => string.Equals(t, tag, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }
        }

        foreach (var pathTerm in parsed.PathTerms)
        {
            if (!(item.Path?.Contains(pathTerm, StringComparison.OrdinalIgnoreCase) ?? false))
            {
                return false;
            }
        }

        foreach (var term in parsed.FreeTerms)
        {
            var hit =
                (item.Name?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (item.Path?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                item.Tags.Any(t => t.Contains(term, StringComparison.OrdinalIgnoreCase));
            if (!hit)
            {
                return false;
            }
        }

        return true;
    }
}
