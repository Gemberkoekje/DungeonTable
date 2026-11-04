using DungeonTable.Web.Services;

namespace DungeonTable.Tests.Web;

/// <summary>Find anything marks the words looked for in each result, whatever their case.</summary>
public sealed class TextHighlightTests
{
    [Fact]
    public void Every_place_a_word_occurs_is_marked_whatever_its_case()
    {
        var parts = TextHighlight.Split("The goblins' fire and the furnace", "FIRE goblin");

        Assert.Equal("The |goblin|s' |fire| and the furnace", string.Join("|", parts.Select(part => part.Text)));
        Assert.Equal("_M_M_", string.Concat(parts.Select(part => part.Marked ? "M" : "_")));
    }

    [Fact]
    public void Overlapping_and_touching_matches_are_one_mark()
    {
        var parts = TextHighlight.Split("bellbell-warden", "bell bel");

        Assert.Equal("bellbell|-warden", string.Join("|", parts.Select(part => part.Text)));
        Assert.True(parts[0].Marked);
        Assert.False(parts[1].Marked);
    }

    [Fact]
    public void A_text_without_the_words_is_one_unmarked_stretch_and_an_empty_one_none()
    {
        HighlightPart only = Assert.Single(TextHighlight.Split("Darkness Below", "ceiling"));
        Assert.Equal("Darkness Below", only.Text);
        Assert.False(only.Marked);

        Assert.Single(TextHighlight.Split("Darkness Below", "  "));
        Assert.Empty(TextHighlight.Split(string.Empty, "ceiling"));
    }
}
