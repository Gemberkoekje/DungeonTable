using System.Collections.Generic;
using DungeonTable.Core.Dossier;

namespace DungeonTable.Tests.Dossiers;

/// <summary>
/// The link markup authors write into dossier prose, read lexically: where each <c>[[…]]</c> sits,
/// its target and shown text, and what is left alone because it only looks like markup.
/// </summary>
public sealed class LinkMarkupTests
{
    private static readonly string[] ThreeTargets = { "a", "b", "c" };

    [Fact]
    public void A_bare_link_is_its_target()
    {
        const string text = "A [[doppelganger]] plays cards.";

        MarkedLink link = Assert.Single(LinkMarkup.Parse(text));

        Assert.Equal("doppelganger", link.Target);
        Assert.Equal(string.Empty, link.Shown);
        Assert.Equal("[[doppelganger]]", text.Substring(link.Start, link.Length));
    }

    [Fact]
    public void A_link_with_shown_text_splits_at_the_first_pipe()
    {
        const string text = "Four [[bandit|human bandits]] play cards.";

        MarkedLink link = Assert.Single(LinkMarkup.Parse(text));

        Assert.Equal("bandit", link.Target);
        Assert.Equal("human bandits", link.Shown);
        Assert.Equal(5, link.Start);
        Assert.Equal("[[bandit|human bandits]]".Length, link.Length);
    }

    [Fact]
    public void Shown_text_may_itself_contain_a_pipe()
    {
        MarkedLink link = Assert.Single(LinkMarkup.Parse("[[a|b|c]]"));

        Assert.Equal("a", link.Target);
        Assert.Equal("b|c", link.Shown);
    }

    [Fact]
    public void A_target_and_its_shown_text_are_trimmed()
    {
        MarkedLink link = Assert.Single(LinkMarkup.Parse("through [[ area 6d | the far door ]]"));

        Assert.Equal("area 6d", link.Target);
        Assert.Equal("the far door", link.Shown);
    }

    [Fact]
    public void Every_link_is_found_in_order()
    {
        IReadOnlyList<MarkedLink> links = LinkMarkup.Parse("[[a]] then [[b|B]], and [[c]].");

        Assert.Equal(ThreeTargets, links.Select(link => link.Target).ToArray());
        Assert.True(links[0].Start < links[1].Start && links[1].Start < links[2].Start);
    }

    [Theory]
    [InlineData("No brackets at all.")]
    [InlineData("An unclosed [[bandit link.")]
    [InlineData("A [single] bracket is prose.")]
    [InlineData("A [[link that runs\nacross two lines]].")]
    [InlineData("A [[target] with one closing bracket.")]
    public void Text_that_only_looks_like_markup_is_left_alone(string text)
    {
        Assert.Empty(LinkMarkup.Parse(text));
        Assert.Equal(text, LinkMarkup.Plain(text));
    }

    [Fact]
    public void A_bracket_inside_a_link_breaks_only_that_link()
    {
        // The outer pair cannot contain a bracket, so only the inner, well-formed link is one.
        MarkedLink link = Assert.Single(LinkMarkup.Parse("[[outer [[inner]] rest]]"));

        Assert.Equal("inner", link.Target);
    }

    [Fact]
    public void An_empty_link_is_still_a_link_so_a_check_can_report_it()
    {
        IReadOnlyList<MarkedLink> links = LinkMarkup.Parse("[[]] and [[ |shown]]");

        Assert.Equal(2, links.Count);
        Assert.All(links, link => Assert.Equal(string.Empty, link.Target));
    }

    [Fact]
    public void Plain_text_shows_each_links_words()
    {
        Assert.Equal(
            "Four human bandits and a doppelganger, by area 6d.",
            LinkMarkup.Plain("Four [[bandit|human bandits]] and a [[doppelganger]], by [[area 6d]]."));
    }

    [Fact]
    public void An_empty_link_shows_its_markup_rather_than_vanishing()
    {
        Assert.Equal("before [[]] after", LinkMarkup.Plain("before [[]] after"));
    }

    [Fact]
    public void Null_and_empty_text_have_no_links()
    {
        Assert.False(LinkMarkup.Contains(null));
        Assert.False(LinkMarkup.Contains(string.Empty));
        Assert.Empty(LinkMarkup.Parse(null));
        Assert.Equal(string.Empty, LinkMarkup.Plain(null));
    }
}
