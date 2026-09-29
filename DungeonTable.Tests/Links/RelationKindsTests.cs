using DungeonTable.Core.Dossier;

namespace DungeonTable.Tests.Links;

/// <summary>
/// The fixed relation vocabulary: sixteen kinds an author may use,
/// <c>related</c> for the book index alone, and how each reads from either end.
/// </summary>
public sealed class RelationKindsTests
{
    private static readonly string[] Sixteen =
    {
        "ally", "rival", "enemy", "family", "leads", "member-of", "serves", "controls", "fears", "hunts",
        "plots-against", "owes", "protects", "loves", "runs", "located-in",
    };

    [Fact]
    public void An_author_has_the_sixteen_kinds_and_not_related()
    {
        Assert.Equal(Sixteen.OrderBy(kind => kind, StringComparer.Ordinal), RelationKinds.Authored.OrderBy(kind => kind, StringComparer.Ordinal));
        Assert.All(Sixteen, kind => Assert.True(RelationKinds.IsAuthored(kind), kind));
        Assert.False(RelationKinds.IsAuthored(RelationKinds.Related));
        Assert.False(RelationKinds.IsAuthored("friend"));
        Assert.False(RelationKinds.IsAuthored("Rival"));
        Assert.False(RelationKinds.IsAuthored(null));
    }

    [Theory]
    [InlineData("ally")]
    [InlineData("rival")]
    [InlineData("enemy")]
    [InlineData("family")]
    [InlineData("related")]
    public void A_mutual_kind_reads_the_same_from_both_ends(string kind)
    {
        Assert.True(RelationKinds.IsMutual(kind));
        Assert.Equal(RelationKinds.Phrase(kind, fromA: true), RelationKinds.Phrase(kind, fromA: false));
    }

    [Theory]
    [InlineData("plots-against", "plots against", "plotted against by")]
    [InlineData("member-of", "member of", "has as a member")]
    [InlineData("protects", "protects", "protected by")]
    [InlineData("located-in", "located in", "home to")]
    [InlineData("runs", "runs", "run by")]
    public void A_directed_kind_reads_differently_from_its_b_end(string kind, string fromA, string fromB)
    {
        Assert.False(RelationKinds.IsMutual(kind));
        Assert.Equal(fromA, RelationKinds.Phrase(kind, fromA: true));
        Assert.Equal(fromB, RelationKinds.Phrase(kind, fromA: false));
    }

    [Fact]
    public void An_unknown_kind_reads_as_written()
    {
        // Shown as written rather than dropped: the content rules report it, the table still sees it.
        Assert.Equal("sworn to", RelationKinds.Phrase("sworn-to", fromA: true));
        Assert.Equal(string.Empty, RelationKinds.Phrase(null, fromA: false));
    }
}
