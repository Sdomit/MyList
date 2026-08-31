using System;
using MyList.Models;
using Xunit;

namespace MyList.Tests.Models;

public class ItemModelTests
{
    [Fact]
    public void AppSettings_DefaultsKeepExistingInteractionAndLauncherCapacity()
    {
        var settings = new AppSettings();

        Assert.False(settings.OpenItemsOnSingleClick);
        Assert.Equal(ViewMode.Grid, settings.ViewMode);
        Assert.Equal(5, settings.MiniLauncherItemLimit);
    }

    [Fact]
    public void Kind_DefaultsToFile_WhenNoFlagsSetAndTypeFile()
    {
        var item = new ItemModel { Type = ItemType.File };
        Assert.Equal(ContentKind.File, item.Kind);
    }

    [Fact]
    public void Kind_ReturnsFolder_WhenTypeFolder()
    {
        var item = new ItemModel { Type = ItemType.Folder };
        Assert.Equal(ContentKind.Folder, item.Kind);
    }

    [Fact]
    public void Kind_AppType_FallsBackToFile()
    {
        var item = new ItemModel { Type = ItemType.App };
        Assert.Equal(ContentKind.File, item.Kind);
    }

    [Fact]
    public void Kind_ActionFlag_TakesPrecedenceOverType()
    {
        var item = new ItemModel { Type = ItemType.Folder, IsActionItem = true };
        Assert.Equal(ContentKind.Action, item.Kind);
    }

    [Fact]
    public void Kind_MtabFlag_TakesPrecedenceOverType()
    {
        var item = new ItemModel { Type = ItemType.File, IsMtab = true };
        Assert.Equal(ContentKind.Mtab, item.Kind);
    }

    [Fact]
    public void Kind_ClipboardText_ReturnsClip()
    {
        var item = new ItemModel { Type = ItemType.File, IsClipboardText = true };
        Assert.Equal(ContentKind.Clip, item.Kind);
    }

    [Fact]
    public void Kind_ClipboardImage_ReturnsClip()
    {
        var item = new ItemModel { Type = ItemType.File, IsClipboardImage = true };
        Assert.Equal(ContentKind.Clip, item.Kind);
    }

    [Fact]
    public void Kind_ActionTakesPrecedenceOverMtab()
    {
        var item = new ItemModel { IsActionItem = true, IsMtab = true };
        Assert.Equal(ContentKind.Action, item.Kind);
    }

    [Fact]
    public void IsPinned_SetTrue_PopulatesPinnedAtUtc()
    {
        var before = DateTime.UtcNow;
        var item = new ItemModel { IsPinned = true };
        var after = DateTime.UtcNow;

        Assert.NotNull(item.PinnedAtUtc);
        Assert.InRange(item.PinnedAtUtc!.Value, before, after);
    }

    [Fact]
    public void IsPinned_SetFalse_ClearsPinnedAtUtc()
    {
        var item = new ItemModel { IsPinned = true };
        Assert.NotNull(item.PinnedAtUtc);

        item.IsPinned = false;
        Assert.Null(item.PinnedAtUtc);
    }

    [Fact]
    public void TrendDelta_EmptyHistory_ReturnsZero()
    {
        var item = new ItemModel();
        Assert.Equal(0, item.TrendDelta);
    }

    [Fact]
    public void TrendDelta_OpensInLast7DaysOnly_ReturnsPositiveCount()
    {
        var item = new ItemModel();
        var now = DateTime.UtcNow;
        item.OpenedHistory.Add(now.AddDays(-1));
        item.OpenedHistory.Add(now.AddDays(-2));
        item.OpenedHistory.Add(now.AddDays(-3));

        // Force trajectory cache invalidation by setting LastOpenedDate
        // Cache populates lazily on first access; no LastOpenedDate trigger needed.

        Assert.True(item.TrendDelta > 0);
    }

    [Fact]
    public void TrendDelta_OpensInPrior7DaysOnly_ReturnsNegative()
    {
        var item = new ItemModel();
        var now = DateTime.UtcNow;
        item.OpenedHistory.Add(now.AddDays(-8));
        item.OpenedHistory.Add(now.AddDays(-9));
        item.OpenedHistory.Add(now.AddDays(-10));
        // Cache populates lazily on first access; no LastOpenedDate trigger needed.

        Assert.True(item.TrendDelta < 0);
    }

    [Fact]
    public void TrendDelta_EqualLastAndPrior_ReturnsZero()
    {
        var item = new ItemModel();
        var now = DateTime.UtcNow;
        item.OpenedHistory.Add(now.AddDays(-1));
        item.OpenedHistory.Add(now.AddDays(-2));
        item.OpenedHistory.Add(now.AddDays(-8));
        item.OpenedHistory.Add(now.AddDays(-9));
        // Cache populates lazily on first access; no LastOpenedDate trigger needed.

        Assert.Equal(0, item.TrendDelta);
    }
}
