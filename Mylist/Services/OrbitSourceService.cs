using System;
using System.Collections.Generic;
using System.Linq;
using MyList.Models;

namespace MyList.Services;

/// <summary>
/// Default <see cref="IOrbitSourceService"/>. Reads everything through delegates so
/// the launcher stays decoupled from <c>MainViewModel</c> and the resolvers stay unit
/// testable. Recent/Trending mirror the app's computed smart lists; Favorites/Pinned
/// are derived from the item set; user collections are matched by id against the
/// supplied snapshot. Smart views the user hid (or that are empty) are dropped from
/// the root ring.
/// </summary>
public sealed class OrbitSourceService : IOrbitSourceService
{
    public const string RecentId = "recent";
    public const string FavoritesId = "fav";
    public const string TrendingId = "trending";
    public const string PinnedId = "pinned";
    public const string HealthId = "health";

    private static readonly (string Id, string Name)[] SmartViews =
    {
        (RecentId, "Recent"),
        (FavoritesId, "Favorites"),
        (TrendingId, "Trending"),
        (PinnedId, "Pinned"),
    };

    private readonly Func<IReadOnlyList<ItemModel>> _allItems;
    private readonly Func<IReadOnlyList<OrbitUserCollection>> _userCollections;
    private readonly Func<IReadOnlyList<ItemModel>> _recent;
    private readonly Func<IReadOnlyList<ItemModel>> _trending;
    private readonly Func<IReadOnlyCollection<string>> _hiddenViews;
    private readonly Func<int> _itemLimit;

    public OrbitSourceService(
        Func<IReadOnlyList<ItemModel>> allItems,
        Func<IReadOnlyList<OrbitUserCollection>> userCollections,
        Func<IReadOnlyList<ItemModel>> recent,
        Func<IReadOnlyList<ItemModel>> trending,
        Func<IReadOnlyCollection<string>> hiddenViews,
        Func<int>? itemLimit = null)
    {
        _allItems = allItems ?? throw new ArgumentNullException(nameof(allItems));
        _userCollections = userCollections ?? throw new ArgumentNullException(nameof(userCollections));
        _recent = recent ?? throw new ArgumentNullException(nameof(recent));
        _trending = trending ?? throw new ArgumentNullException(nameof(trending));
        _hiddenViews = hiddenViews ?? throw new ArgumentNullException(nameof(hiddenViews));
        _itemLimit = itemLimit ?? (() => 5);
    }

    public IReadOnlyList<ItemModel> GetAllItems() => Items().ToList();

    public IReadOnlyList<OrbitCollection> GetRootCollections()
    {
        var hidden = _hiddenViews();
        var ring = new List<OrbitCollection>();

        foreach (var (id, name) in SmartViews)
        {
            if (hidden.Contains(id))
            {
                continue;
            }

            var view = new OrbitCollection { Id = id, Name = name, IsSmart = true };
            if (GetItems(view).Count > 0)
            {
                ring.Add(view);
            }
        }

        ring.AddRange(_userCollections()
            .Where(c => c.Items.Any(i => !i.IsClipboardImage))
            .Select(c => new OrbitCollection { Id = c.Id.ToString(), Name = c.Name }));

        return ring.Take(ItemLimit).ToList();
    }

    public IReadOnlyList<ItemModel> GetItems(OrbitCollection collection)
    {
        if (collection is null)
        {
            return Array.Empty<ItemModel>();
        }

        return collection.Id switch
        {
            RecentId => _recent().Where(i => !i.IsClipboardImage).Take(ItemLimit).ToList(),
            TrendingId => _trending().Where(i => !i.IsClipboardImage).Take(ItemLimit).ToList(),
            FavoritesId => Items()
                .Where(i => i.IsFavorite)
                .OrderByDescending(i => i.LastOpenedDate)
                .Take(ItemLimit)
                .ToList(),
            PinnedId => Items()
                .Where(i => i.IsPinned)
                .OrderByDescending(i => i.PinnedAtUtc ?? DateTime.MinValue)
                .Take(ItemLimit)
                .ToList(),
            HealthId => Items()
                .Where(i => i.HealthState is ItemHealthState.Offline
                                          or ItemHealthState.Missing
                                          or ItemHealthState.PermissionDenied)
                .Take(ItemLimit)
                .ToList(),
            _ => ResolveUserCollection(collection.Id),
        };
    }

    private IReadOnlyList<ItemModel> ResolveUserCollection(string id)
    {
        var snapshot = _userCollections().FirstOrDefault(c => c.Id.ToString() == id);
        return snapshot is null
            ? Array.Empty<ItemModel>()
            : snapshot.Items.Where(i => !i.IsClipboardImage).Take(ItemLimit).ToList();
    }

    private IEnumerable<ItemModel> Items() => _allItems().Where(i => !i.IsClipboardImage);

    private int ItemLimit => Math.Clamp(_itemLimit(), 3, 7);
}
