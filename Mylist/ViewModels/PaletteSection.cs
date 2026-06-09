using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MyList.ViewModels;

public sealed class PaletteSection : ViewModelBase
{
    public PaletteSection(string title)
    {
        Title = title;
        Rows = new ObservableCollection<IPaletteRow>();
    }

    public string Title { get; }

    public ObservableCollection<IPaletteRow> Rows { get; }

    public bool HasResults => Rows.Count > 0;

    public void Replace(IList<IPaletteRow> next)
    {
        Rows.Clear();
        for (var i = 0; i < next.Count; i++)
        {
            Rows.Add(next[i]);
        }

        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(Rows));
    }
}
