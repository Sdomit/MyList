using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using MyList.Models;
using MyList.Services;
using Xunit;

namespace MyList.Tests.Services;

public class OrbitSourceServiceTests
{
    private static ItemModel Item(
        string name,
        bool favorite = false,
        bool pinned = false,
        DateTime? lastOpened = null,
        ItemHealthState health = ItemHealthState.Healthy)
        => new()
        {
            Name = name,
            Path = $@"C:\{name}",
            IsFavorite = favorite,
            IsPinned = pinned,
            LastOpenedDate = lastOpened ?? default,
            HealthState = health,
        };

    private static OrbitSourceService Build(
        IReadOnlyList<ItemModel> items,
        IReadOnlyList<OrbitUserCollection>? collections = null)
        => new(() => items, () => collections ?? Array.Empty<OrbitUserCollection>());

    [Fact]
    public void GetItems_SmartRecent_ReturnsMostRecentlyOpenedFirst()
    {
        var older = Item("older", lastOpened: new DateTime(2026, 1, 1));
        var newer = Item("newer", lastOpened: new DateTime(2026, 6, 1));
        var never = Item("never");
        var service = Build(new[] { older, newer, never });

        var recent = service.GetItems(new OrbitCollection { Id = OrbitSourceService.RecentId });

        recent.Should().NotContain(never);
        recent.First().Should().BeSameAs(newer);
    }

    [Fact]
    public void GetItems_SmartHealth_ReturnsUnhealthyOnly()
    {
        var ok = Item("ok", health: ItemHealthState.Healthy);
        var offline = Item("offline", health: ItemHealthState.Offline);
        var missing = Item("missing", health: ItemHealthState.Missing);
        var unchecked_ = Item("unchecked", health: ItemHealthState.Unchecked);
        var service = Build(new[] { ok, offline, missing, unchecked_ });

        var unhealthy = service.GetItems(new OrbitCollection { Id = OrbitSourceService.HealthId });

        unhealthy.Should().BeEquivalentTo(new[] { offline, missing });
    }

    [Fact]
    public void GetItems_SmartFavorites_ReturnsFavoritesOnly()
    {
        var fav = Item("fav", favorite: true);
        var plain = Item("plain");
        var service = Build(new[] { fav, plain });

        var favorites = service.GetItems(new OrbitCollection { Id = OrbitSourceService.FavoritesId });

        favorites.Should().ContainSingle().Which.Should().BeSameAs(fav);
    }

    [Fact]
    public void GetItems_UserCollection_ResolvesByIdAndCapsAtFive()
    {
        var id = Guid.NewGuid();
        var members = Enumerable.Range(0, 7).Select(i => Item($"m{i}")).ToList();
        var service = Build(
            members,
            new[] { new OrbitUserCollection(id, "Work", members) });

        var items = service.GetItems(new OrbitCollection { Id = id.ToString() });

        items.Should().HaveCount(5);
    }

    [Fact]
    public void GetRootCollections_LeadsWithSmartThenNonEmptyUserCollections()
    {
        var member = Item("m");
        var full = new OrbitUserCollection(Guid.NewGuid(), "Work", new[] { member });
        var empty = new OrbitUserCollection(Guid.NewGuid(), "Empty", Array.Empty<ItemModel>());
        var service = Build(new[] { member }, new[] { full, empty });

        var root = service.GetRootCollections();

        root.Select(c => c.Name).Should().StartWith(new[] { "Recent", "Favorites", "Work" });
        root.Select(c => c.Name).Should().NotContain("Empty");
        root.Should().HaveCountLessOrEqualTo(5);
    }

    [Fact]
    public void GetAllItems_ExcludesClipboardImages()
    {
        var file = Item("file");
        var image = Item("image");
        image.IsClipboardImage = true;
        var service = Build(new[] { file, image });

        service.GetAllItems().Should().ContainSingle().Which.Should().BeSameAs(file);
    }
}
