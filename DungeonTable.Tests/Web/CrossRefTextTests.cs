using DungeonTable.Core.Dossier;
using DungeonTable.Web.Services;

namespace DungeonTable.Tests.Web;

/// <summary>
/// How a block of prose is split around its references: a link shows the words its reference
/// carries, which for an authored link are not the characters of its span.
/// </summary>
public sealed class CrossRefTextTests
{
    private const string Body = "Four [[bandit|human bandits]] guard [[area 6d]].";

    [Fact]
    public void A_link_shows_its_references_words_and_keeps_the_markup_as_its_source()
    {
        CrossRefText.Segment link = CrossRefText.Split(Body, Refs())[1];

        Assert.True(link.IsLink);
        Assert.Equal("human bandits", link.Text);
        Assert.Equal("[[bandit|human bandits]]", link.Source);
        Assert.Equal("bandit-node", link.TargetId);
    }

    [Fact]
    public void A_broken_link_is_a_segment_of_its_own_but_not_a_link()
    {
        CrossRefText.Segment broken = CrossRefText.Split(Body, Refs())[3];

        Assert.False(broken.IsLink);
        Assert.Equal(CrossRefKind.Unresolved, broken.Kind);
        Assert.Equal("area 6d", broken.Text);
        Assert.Equal("[[area 6d]]", broken.Source);
    }

    [Fact]
    public void A_pending_link_is_a_segment_of_its_own_but_not_a_link()
    {
        const string text = "Stairs to [[L2 area 1]].";
        var pending = new CrossRef { Text = "L2 area 1", Kind = CrossRefKind.Pending, TargetId = string.Empty, Start = 10, Length = 13 };

        CrossRefText.Segment segment = CrossRefText.Split(text, new[] { pending })[1];

        Assert.False(segment.IsLink);
        Assert.Equal(CrossRefKind.Pending, segment.Kind);
        Assert.Equal("L2 area 1", segment.Text);
        Assert.Equal("Stairs to L2 area 1.", CrossRefText.Display(text, new[] { pending }));
    }

    [Fact]
    public void The_display_text_has_every_reference_replaced_by_its_words()
    {
        Assert.Equal("Four human bandits guard area 6d.", CrossRefText.Display(Body, Refs()));
    }

    [Fact]
    public void A_name_found_in_prose_shows_as_written()
    {
        const string text = "The bugbears attack.";
        var found = new CrossRef { Text = "bugbears", Kind = CrossRefKind.Monster, TargetId = "bugbear-node", Start = 4, Length = 8 };

        Assert.Equal(text, CrossRefText.Display(text, new[] { found }));
        Assert.Equal("bugbears", CrossRefText.Split(text, new[] { found })[1].Text);
    }

    [Fact]
    public void Prose_with_no_references_is_one_plain_segment()
    {
        CrossRefText.Segment only = Assert.Single(CrossRefText.Split("Dust.", Array.Empty<CrossRef>()));

        Assert.False(only.IsLink);
        Assert.Equal("Dust.", only.Text);
    }

    private static CrossRef[] Refs() => new[]
    {
        new CrossRef { Text = "human bandits", Kind = CrossRefKind.Monster, TargetId = "bandit-node", Start = 5, Length = 24 },
        new CrossRef { Text = "area 6d", Kind = CrossRefKind.Unresolved, TargetId = string.Empty, Start = 36, Length = 11 },
    };
}
