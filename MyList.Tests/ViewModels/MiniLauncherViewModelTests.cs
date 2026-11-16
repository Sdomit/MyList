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

    [Fact]
    public void EmptyRing_HasNoMoreSlotAndShowsEmptyState()
    {
        var viewModel = new MiniLauncherViewModel(
            new TestOrbitSource(Array.Empty<OrbitCollection>()),
            new LauncherService(),
            () => { },
            () => { },
            () => 7,
            _ => { },
            _ => { },
            _ => { });

        viewModel.VisibleSlots.Should().BeEmpty();
        viewModel.IsRingEmpty.Should().BeTrue();
    }

    [Fact]
    public void Search_UpDownMovesSelectionAndWraps()
    {
        var items = Enumerable.Range(1, 3)
            .Select(index => new ItemModel { Name = $"Alpha {index}", Path = $@"C:lpha{index}" })
            .ToList();
        var viewModel = new MiniLauncherViewModel(
            new TestOrbitSource(Array.Empty<OrbitCollection>(), items),
            new LauncherService(),
            () => { },
            () => { },
            () => 7,
            _ => { },
            _ => { },
            _ => { });

        viewModel.SearchText = "alpha";
        viewModel.SearchResults.Should().HaveCount(3);

        viewModel.MoveResultSelection(1);
        viewModel.SelectedResultIndex.Should().Be(0);
        viewModel.SearchResults[0].IsSelected.Should().BeTrue();

        viewModel.MoveResultSelection(-1);
        viewModel.SelectedResultIndex.Should().Be(2);
        viewModel.SearchResults[0].IsSelected.Should().BeFalse();
        viewModel.SearchResults[2].IsSelected.Should().BeTrue();
    }

    private sealed class TestOrbitSource(IReadOnlyList<OrbitCollection> rootCollections, IReadOnlyList<ItemModel>? allItems = null) : IOrbitSourceService
    {
        public IReadOnlyList<OrbitCollection> GetRootCollections() => rootCollections;

        public IReadOnlyList<ItemModel> GetItems(OrbitCollection collection) => Array.Empty<ItemModel>();

        public IReadOnlyList<ItemModel> GetAllItems() => allItems ?? Array.Empty<ItemModel>();
    }
}
