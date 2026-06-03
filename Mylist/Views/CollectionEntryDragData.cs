using MyList.ViewModels;

namespace MyList.Views;

public sealed record CollectionEntryDragData(CollectionEntryViewModel Entry, CollectionViewModel? SourceCollection);
