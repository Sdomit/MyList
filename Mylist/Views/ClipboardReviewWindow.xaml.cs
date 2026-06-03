using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using MyList.Models;
using MyList.ViewModels;

namespace MyList.Views;

public partial class ClipboardReviewWindow : Window
{
    private readonly IReadOnlyList<ClipboardReviewEntry> _sourceEntries;

    public ClipboardReviewWindow(
        IReadOnlyList<ClipboardReviewEntry> entries,
        IReadOnlyList<CollectionViewModel> availableCollections,
        CollectionViewModel? selectedCollection,
        IReadOnlyList<ClipboardImportHistoryEntry> historyEntries)
    {
        InitializeComponent();

        _sourceEntries = entries;
        AvailableCollections = new ObservableCollection<CollectionViewModel>(availableCollections);
        HistoryEntries = new ObservableCollection<ClipboardImportHistoryEntry>(historyEntries);

        SelectedTargetCollection = selectedCollection ?? AvailableCollections.FirstOrDefault();

        WillAddEntries = new ObservableCollection<ClipboardReviewEntry>();
        WillLinkEntries = new ObservableCollection<ClipboardReviewEntry>();
        SkippedEntries = new ObservableCollection<ClipboardReviewEntry>();

        RefreshEntryViews();

        DataContext = this;
    }

    public ObservableCollection<CollectionViewModel> AvailableCollections { get; }

    public ObservableCollection<ClipboardReviewEntry> WillAddEntries { get; }

    public ObservableCollection<ClipboardReviewEntry> WillLinkEntries { get; }

    public ObservableCollection<ClipboardReviewEntry> SkippedEntries { get; }

    public ObservableCollection<ClipboardImportHistoryEntry> HistoryEntries { get; }

    public bool UseHistorySelection { get; private set; }

    public CollectionViewModel? SelectedTargetCollection { get; set; }

    public ClipboardImportHistoryEntry? SelectedHistoryEntry { get; set; }

    public bool CanReapplyHistory => SelectedHistoryEntry is not null;

    public int ApplyCount => WillAddEntries.Count + WillLinkEntries.Count;

    public bool CanConfirm => ApplyCount > 0;

    public string ConfirmButtonText => ApplyCount > 0
        ? $"Add {ApplyCount} Item{(ApplyCount == 1 ? string.Empty : "s")}" 
        : "No Valid Paths";

    public string SummaryText => $"Will add {WillAddEntries.Count}, link {WillLinkEntries.Count}, skip {SkippedEntries.Count}.";

    public string WillAddHeader => $"Will Add ({WillAddEntries.Count})";

    public string WillLinkHeader => $"Will Link Existing ({WillLinkEntries.Count})";

    public string SkippedHeader => $"Skipped ({SkippedEntries.Count})";

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void OnConfirm(object sender, RoutedEventArgs e)
    {
        if (!CanConfirm)
        {
            return;
        }

        UseHistorySelection = false;
        DialogResult = true;
    }

    private void OnReapplyHistory(object sender, RoutedEventArgs e)
    {
        if (SelectedHistoryEntry is null)
        {
            return;
        }

        UseHistorySelection = true;
        DialogResult = true;
    }

    private void RefreshEntryViews()
    {
        WillAddEntries.Clear();
        WillLinkEntries.Clear();
        SkippedEntries.Clear();

        foreach (var entry in _sourceEntries)
        {
            if (entry.Status == ClipboardReviewStatus.ValidNew && entry.WillApply)
            {
                WillAddEntries.Add(entry);
                continue;
            }

            if (entry.Status == ClipboardReviewStatus.ValidExisting && entry.WillApply)
            {
                WillLinkEntries.Add(entry);
                continue;
            }

            if (!entry.WillApply)
            {
                SkippedEntries.Add(entry);
            }
        }
    }
}
