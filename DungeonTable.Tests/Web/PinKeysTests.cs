using DungeonTable.Web.Services;

namespace DungeonTable.Tests.Web;

/// <summary>A pin says whether it names a card or a passage, so the strip knows how to show it.</summary>
public sealed class PinKeysTests
{
    [Fact]
    public void A_card_and_a_passage_are_told_apart_by_their_prefix()
    {
        string card = PinKeys.Card(" grask ");
        string passage = PinKeys.Passage("rules/darkness-below");

        Assert.Equal("card:grask", card);
        Assert.True(PinKeys.IsCard(card));
        Assert.False(PinKeys.IsPassage(card));
        Assert.Equal("grask", PinKeys.Target(card));

        Assert.Equal("text:rules/darkness-below", passage);
        Assert.True(PinKeys.IsPassage(passage));
        Assert.False(PinKeys.IsCard(passage));
        Assert.Equal("rules/darkness-below", PinKeys.Target(passage));
    }

    [Fact]
    public void A_pin_that_is_neither_names_nothing()
    {
        Assert.Equal(string.Empty, PinKeys.Target("grask"));
        Assert.Equal(string.Empty, PinKeys.Target(null));
        Assert.False(PinKeys.IsCard(null));
    }
}
