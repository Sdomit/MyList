using System;
using System.Collections.Generic;
using System.Linq;
using MyList.Models;

namespace MyList.Services;

/// <summary>
/// Default <see cref="IOrbitSourceService"/>. Reads everything through delegates so
/// the launcher stays decoupled from <c>MainViewModel</c> and the resolvers stay unit
/// testable. Smart collections (Recent, Favorites, …) are computed from the item set;
/// user collections are matched by id against the supplied snapshot.
/// </summary>
public sealed class OrbitSourceService : IOrbitSourceService
{
    public const string RecentId = "recent";
    public const string FavoritesId = "fav";
    public const string PinnedId = "pinned";
    public const string HealthId = "health";

    private const int RingCap = 5;

    private readonly Func<IReadOnlyList<ItemModel>> _allItems;
    private readonly Func<IReadOnlyList<OrbitUserCollection>> _userCollections;

    public OrbitSourceService(
        Func<IReadOnlyList<ItemModel>> allItems,
        Func<IReadOnlyList<OrbitUserCollection>> userCollections)
    {
        _allItems = allItems ?? throw new ArgumentNullException(nameof(allItems));
        _userCollections = userCollections ?? throw new ArgumentNullException(nameof(userCollections));
    }

    public IReadOnlyList<ItemModel> GetAllItems() => Items().ToList();

    public IReadOnlyList<OrbitCollection> GetRootCollections()
    {
        var ring = new List<OrbitCollection>
        {
            new() { Id = RecentId, Name = "Recent", IsSmart = true },
            new() { Id = FavoritesId, Name = "Favorites", IsSmart = true },
        };

        ring.AddRange(_userCollections()
            .Where(c => c.Items.Any(i => !i.IsClipboardImage))
            .Select(c => new OrbitCollection { Id = c.Id.ToString(), Name = c.Name }));

        return ring.Take(RingCap).ToList();
    }

    public IReadOnlyList<ItemModel> GetItems(OrbitCollection collection)
    {
        if (collection is null)
        {
            return Array.Empty<ItemModel>();
        }

        return collection.Id switch
        {
            RecentId => Items()
                .Where(i => i.LastOpenedDate != default)
                .OrderByDescending(i => i.LastOpenedDate)
                .Take(RingCap)
                .ToList(),
            FavoritesId => Items()
                .Where(i => i.IsFavorite)
                .OrderByDescending(i => i.LastOpenedDate)
                .Take(RingCap)
                .ToList(),
            PinnedId => Items()
                .Where(i => i.IsPinned)
                .OrderByDescending(i => i.PinnedAtUtc ?? DateTime.MinValue)
                .Take(RingCap)
                .ToList(),
            HealthId => Items()
                .Where(i => i.HealthState is ItemHealthState.Offline
                                          or ItemHealthState.Missing
                                          or ItemHealthState.PermissionDenied)
                .Take(RingCap)
                .ToList(),
            _ => ResolveUserCollection(collection.Id),
        };
    }

    private IReadOnlyList<ItemModel> ResolveUserCollection(string id)
    {
        var snapshot = _userCollections().FirstOrDefault(c => c.Id.ToString() == id);
        return snapshot is null
            ? Array.Empty<ItemModel>()
            : snapshot.Items.Where(i => !i.IsClipboardImage).Take(RingCap).ToList();
    }

    private IEnumerable<ItemModel> Items() => _allItems().Where(i => !i.IsClipboardImage);
}
