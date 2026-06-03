using System;
using System.Collections.ObjectModel;
using MyList.Helpers;

namespace MyList.Models;

public sealed class CollectionModel : ObservableObject
{
    private string _name = "New Collection";
    private bool _isSmart;
    private SmartCollectionType _smartType;
    private string _smartValue = string.Empty;

    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public ObservableCollection<Guid> ItemIds { get; set; } = new();

    public ObservableCollection<CollectionEntryModel> Entries { get; set; } = new();

    public bool IsSmart
    {
        get => _isSmart;
        set => SetProperty(ref _isSmart, value);
    }

    public SmartCollectionType SmartType
    {
        get => _smartType;
        set => SetProperty(ref _smartType, value);
    }

    public string SmartValue
    {
        get => _smartValue;
        set => SetProperty(ref _smartValue, value);
    }
}
