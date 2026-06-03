using System;
using MyList.Helpers;

namespace MyList.Models;

public sealed class CollectionEntryModel : ObservableObject
{
    private CollectionEntryKind _kind;
    private Guid _itemId;

    public Guid Id { get; set; } = Guid.NewGuid();

    public CollectionEntryKind Kind
    {
        get => _kind;
        set => SetProperty(ref _kind, value);
    }

    public Guid ItemId
    {
        get => _itemId;
        set => SetProperty(ref _itemId, value);
    }
}
