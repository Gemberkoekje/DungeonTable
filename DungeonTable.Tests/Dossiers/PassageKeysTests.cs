using DungeonTable.Core.Dossier;

namespace DungeonTable.Tests.Dossiers;

/// <summary>
/// The keys passages are found, opened and pinned by: made of where a block sits and its heading, so they
/// survive an edit of the text and a block inserted above.
/// </summary>
public sealed class PassageKeysTests
{
    private static readonly string[] Inserted = { "rules/resting", "rules/darkness-below" };

    [Theory]
    [InlineData("Grask's crew", "grasks-crew")]
    [InlineData("1-4: Goblin Fetchers", "1-4-goblin-fetchers")]
    [InlineData("  Doors and Secret Doors  ", "doors-and-secret-doors")]
    [InlineData("Bell-Founder’s Workshop", "bell-founders-workshop")]
    [InlineData("???", "untitled")]
    [InlineData("", "untitled")]
    public void A_heading_becomes_a_lower_case_slug(string heading, string slug) =>
        Assert.Equal(slug, PassageKeys.Slug(heading));

    [Fact]
    public void A_block_keeps_its_key_when_another_is_put_above_it()
    {
        DossierBlock darkness = new() { Heading = "Darkness Below", Body = "No light." };
        DossierBlock resting = new() { Heading = "Resting", Body = "Short rests are safe." };

        Assert.Equal("rules/darkness-below", PassageKeys.Rules(new[] { darkness })[0]);
        Assert.Equal(Inserted, PassageKeys.Rules(new[] { resting, darkness }));
    }

    [Fact]
    public void A_floors_facts_are_keyed_under_the_floor()
    {
        DossierBlock crew = new() { Id = "grasks-crew", Heading = "The Bugbears" };

        Assert.Equal("level/data_dossiers_level_1/what-dwells-here", PassageKeys.LevelOverview("data_dossiers_level_1"));
        Assert.Equal("level/data_dossiers_level_1/aftermath", PassageKeys.LevelAftermath("data_dossiers_level_1"));
        Assert.Equal("level/data_dossiers_level_1/factions/grasks-crew", PassageKeys.Factions("data_dossiers_level_1", new[] { crew })[0]);
        Assert.Equal("level/data_dossiers_level_1/wandering/the-bugbears", PassageKeys.WanderingMonsters("data_dossiers_level_1", new[] { new DossierBlock { Heading = "The Bugbears" } })[0]);
    }

    [Fact]
    public void A_key_already_given_out_gets_a_number()
    {
        var used = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);

        Assert.Equal("rules/doors", PassageKeys.Unique("rules/doors", used));
        Assert.Equal("rules/doors-2", PassageKeys.Unique("rules/doors", used));
        Assert.Equal("rules/doors-3", PassageKeys.Unique("rules/doors", used));
        Assert.Equal(3, used.Count);
    }
}
