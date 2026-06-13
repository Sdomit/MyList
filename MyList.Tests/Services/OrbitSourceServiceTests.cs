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
        IReadOnlyList<OrbitUserCollection>? collections = null,
        IReadOnlyList<ItemModel>? recent = null,
        IReadOnlyList<ItemModel>? trending = null,
        IReadOnlyCollection<string>? hidden = null)
        => new(
            () => items,
            () => collections ?? Array.Empty<OrbitUserCollection>(),
            () => recent ?? Array.Empty<ItemModel>(),
            () => trending ?? Array.Empty<ItemModel>(),
            () => hidden ?? Array.Empty<string>());

    [Fact]
    public void GetItems_SmartRecent_ReturnsProvidedRecentList()
    {
        var a = Item("a");
        var b = Item("b");
        var service = Build(new[] { a, b }, recent: new[] { b, a });

        var recent = service.GetItems(new OrbitCollection { Id = OrbitSourceService.RecentId });

        recent.Should().Equal(b, a);
    }

    [Fact]
    public void GetItems_SmartTrending_ReturnsProvidedTrendingList()
    {
        var t = Item("trending");
        var service = Build(new[] { t }, trending: new[] { t });

        var trending = service.GetItems(new OrbitCollection { Id = OrbitSourceService.TrendingId });

        trending.Should().ContainSingle().Which.Should().BeSameAs(t);
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
    public void GetItems_SmartPinned_ReturnsPinnedOnly()
    {
        var pinned = Item("pinned", pinned: true);
        var plain = Item("plain");
        var service = Build(new[] { pinned, plain });

        var result = service.GetItems(new OrbitCollection { Id = OrbitSourceService.PinnedId });

        result.Should().ContainSingle().Which.Should().BeSameAs(pinned);
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
    public void GetRootCollections_IncludesOnlyNonEmptySmartViews()
    {
        // Only Recent has content; Favorites/Trending/Pinned are empty.
        var member = Item("m");
        var recentItem = Item("r");
        var work = new OrbitUserCollection(Guid.NewGuid(), "Work", new[] { member });
        var service = Build(new[] { member, recentItem }, new[] { work }, recent: new[] { recentItem });

        var names = service.GetRootCollections().Select(c => c.Name).ToList();

        names.Should().Equal("Recent", "Work");
        names.Should().NotContain(new[] { "Favorites", "Trending", "Pinned" });
    }

    [Fact]
    public void GetRootCollections_HiddenSmartViewIsExcluded()
    {
        var recentItem = Item("r");
        var service = Build(
            new[] { recentItem },
            recent: new[] { recentItem },
            hidden: new[] { OrbitSourceService.RecentId });

        service.GetRootCollections().Select(c => c.Name).Should().NotContain("Recent");
    }

    [Fact]
    public void GetRootCollections_CapsAtFive()
    {
        var fav = Item("f", favorite: true);
        var pin = Item("p", pinned: true);
        var rec = Item("r");
        var tre = Item("t");
        var members = new[] { Item("m") };
        var collections = Enumerable.Range(0, 4)
            .Select(i => new OrbitUserCollection(Guid.NewGuid(), $"C{i}", members))
            .ToList();
        var service = Build(
            new[] { fav, pin },
            collections,
            recent: new[] { rec },
            trending: new[] { tre });

        service.GetRootCollections().Should().HaveCount(5);
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
