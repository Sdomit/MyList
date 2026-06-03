using MyList.Models;
using MyList.ViewModels;

namespace MyList.Views;

public sealed record CollectionItemDragData(ItemModel Item, CollectionViewModel? SourceCollection);
