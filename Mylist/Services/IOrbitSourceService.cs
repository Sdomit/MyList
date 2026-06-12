using System;
using System.Collections.Generic;
using MyList.Models;

namespace MyList.Services;

/// <summary>
/// One node on the root (collection) ring. Smart collections (Recent, Favorites,
/// …) and user collections share this shape; <see cref="Id"/> drives the resolver
/// in <see cref="IOrbitSourceService.GetItems"/>.
/// </summary>
public sealed class OrbitCollection
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public bool IsSmart { get; init; }
}

/// <summary>
/// Snapshot of one user-defined collection handed to <see cref="OrbitSourceService"/>
/// at summon time so the launcher never holds live references to view-models.
/// </summary>
public sealed record OrbitUserCollection(Guid Id, string Name, IReadOnlyList<ItemModel> Items);

/// <summary>
/// Two-level data source for the mini-launcher orbit:
/// ring 1 = collections, ring 2 = the items inside a drilled-in collection.
/// </summary>
public interface IOrbitSourceService
{
    /// <summary>Up to 5 collections for the root ring (slot 6 is the launcher's "More…").</summary>
    IReadOnlyList<OrbitCollection> GetRootCollections();

    /// <summary>Up to 5 items inside <paramref name="collection"/>.</summary>
    IReadOnlyList<ItemModel> GetItems(OrbitCollection collection);

    /// <summary>Every launchable item, used by the global search overlay.</summary>
    IReadOnlyList<ItemModel> GetAllItems();
}
