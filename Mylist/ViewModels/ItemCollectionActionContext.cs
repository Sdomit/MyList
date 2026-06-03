using MyList.Models;

namespace MyList.ViewModels;

public sealed record ItemCollectionActionContext(ItemModel? Item, CollectionViewModel? Collection);
