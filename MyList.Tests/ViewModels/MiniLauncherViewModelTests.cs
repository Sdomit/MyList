using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using MyList.Models;
using MyList.Services;
using MyList.ViewModels;
using Xunit;

namespace MyList.Tests.ViewModels;

public class MiniLauncherViewModelTests
{
    [Fact]
    public void RootRing_UsesConfiguredLimitAndReservesMoreSlot()
    {
        var source = new TestOrbitSource(Enumerable.Range(1, 9)
            .Select(index => new OrbitCollection { Id = index.ToString(), Name = $"Collection {index}" })
            .ToList());

        var viewModel = new MiniLauncherViewModel(
            source,
            new LauncherService(),
            () => { },
            () => { },
            () => 7,
            _ => { },
            _ => { },
            _ => { });

        viewModel.VisibleSlots.Should().HaveCount(8);
        viewModel.VisibleSlots.Last().IsMore.Should().BeTrue();
        viewModel.MaxShortcutIndex.Should().Be(8);
        viewModel.ShortcutRangeHint.Should().Be("1–8");
    }

    private sealed class TestOrbitSource(IReadOnlyList<OrbitCollection> rootCollections) : IOrbitSourceService
    {
        public IReadOnlyList<OrbitCollection> GetRootCollections() => rootCollections;

        public IReadOnlyList<ItemModel> GetItems(OrbitCollection collection) => Array.Empty<ItemModel>();

        public IReadOnlyList<ItemModel> GetAllItems() => Array.Empty<ItemModel>();
    }
}
