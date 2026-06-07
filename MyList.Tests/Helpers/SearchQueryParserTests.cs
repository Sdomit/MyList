using MyList.Helpers;
using MyList.Models;
using Xunit;

namespace MyList.Tests.Helpers;

public class SearchQueryParserTests
{
    [Fact]
    public void Parse_NullOrWhitespace_ReturnsEmptyQuery()
    {
        Assert.True(SearchQueryParser.Parse(null).IsEmpty);
        Assert.True(SearchQueryParser.Parse(string.Empty).IsEmpty);
        Assert.True(SearchQueryParser.Parse("   ").IsEmpty);
    }

    [Fact]
    public void Parse_FreeTerms_CollectsAllWhitespaceSeparatedTokens()
    {
        var query = SearchQueryParser.Parse("alpha bravo charlie");
        Assert.Equal(new[] { "alpha", "bravo", "charlie" }, query.FreeTerms);
        Assert.Empty(query.Tags);
        Assert.Empty(query.PathTerms);
        Assert.Null(query.ItemType);
        Assert.False(query.IsEmpty);
    }

    [Fact]
    public void Parse_TagOperator_FillsTagsList()
    {
        var query = SearchQueryParser.Parse("tag:work tag:urgent free");
        Assert.Equal(new[] { "work", "urgent" }, query.Tags);
        Assert.Equal(new[] { "free" }, query.FreeTerms);
    }

    [Fact]
    public void Parse_PathOperator_FillsPathTerms()
    {
        var query = SearchQueryParser.Parse("path:projects path:src");
        Assert.Equal(new[] { "projects", "src" }, query.PathTerms);
    }

    [Theory]
    [InlineData("type:folder", ItemType.Folder)]
    [InlineData("type:Folder", ItemType.Folder)]
    [InlineData("type:dir", ItemType.Folder)]
    [InlineData("type:directory", ItemType.Folder)]
    [InlineData("type:file", ItemType.File)]
    [InlineData("type:File", ItemType.File)]
    [InlineData("type:app", ItemType.App)]
    [InlineData("type:exe", ItemType.App)]
    [InlineData("type:application", ItemType.App)]
    public void Parse_TypeOperator_AcceptsAliases(string input, ItemType expected)
    {
        var query = SearchQueryParser.Parse(input);
        Assert.Equal(expected, query.ItemType);
        Assert.Empty(query.InvalidTokens);
    }

    [Fact]
    public void Parse_UnknownTypeAlias_LandsInInvalidTokens()
    {
        var query = SearchQueryParser.Parse("type:gizmo");
        Assert.Null(query.ItemType);
        Assert.Equal(new[] { "type:gizmo" }, query.InvalidTokens);
    }

    [Theory]
    [InlineData("offline:true", true)]
    [InlineData("offline:false", false)]
    [InlineData("offline:1", true)]
    [InlineData("offline:0", false)]
    [InlineData("offline:yes", true)]
    [InlineData("offline:no", false)]
    [InlineData("offline:Y", true)]
    [InlineData("offline:n", false)]
    public void Parse_OfflineOperator_AcceptsBooleanAliases(string input, bool expected)
    {
        var query = SearchQueryParser.Parse(input);
        Assert.Equal(expected, query.IsOffline);
    }

    [Theory]
    [InlineData("fav:true", true)]
    [InlineData("favorite:false", false)]
    public void Parse_FavOperator_AcceptsBothKeysAndBooleanAliases(string input, bool expected)
    {
        var query = SearchQueryParser.Parse(input);
        Assert.Equal(expected, query.IsFavorite);
    }

    [Fact]
    public void Parse_UnknownKey_RecordsInvalidToken()
    {
        var query = SearchQueryParser.Parse("flavor:vanilla");
        Assert.Equal(new[] { "flavor:vanilla" }, query.InvalidTokens);
        Assert.True(query.IsEmpty);
    }

    [Fact]
    public void Parse_KeyWithEmptyValue_RecordsInvalidToken()
    {
        var query = SearchQueryParser.Parse("tag:");
        Assert.Equal(new[] { "tag:" }, query.InvalidTokens);
        Assert.Empty(query.Tags);
    }

    [Fact]
    public void Parse_MixedQuery_SeparatesEachBucket()
    {
        var query = SearchQueryParser.Parse("notes tag:work type:file offline:false path:projects unknown:x");
        Assert.Equal(new[] { "notes" }, query.FreeTerms);
        Assert.Equal(new[] { "work" }, query.Tags);
        Assert.Equal(new[] { "projects" }, query.PathTerms);
        Assert.Equal(ItemType.File, query.ItemType);
        Assert.Equal(false, query.IsOffline);
        Assert.Equal(new[] { "unknown:x" }, query.InvalidTokens);
        Assert.False(query.IsEmpty);
    }

    [Fact]
    public void Parse_OnlyInvalidTokens_IsEmptyReportedTrue()
    {
        // IsEmpty ignores invalid tokens by design - it only reflects what survived to filter on.
        var query = SearchQueryParser.Parse("unknown:x flavor:vanilla");
        Assert.True(query.IsEmpty);
        Assert.Equal(2, query.InvalidTokens.Count);
    }
}
