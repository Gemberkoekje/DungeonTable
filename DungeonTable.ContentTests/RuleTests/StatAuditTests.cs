using System.Collections.Generic;
using DungeonTable.ContentTests.Rules;
using DungeonTable.Core.Dossier;
using DungeonTable.Core.Stats;

namespace DungeonTable.ContentTests.RuleTests;

/// <summary>
/// Proves the stat-block rules catch an entry the library would drop, a monster no monster could be, a
/// spell it casts that is missing, and a source no book gives a title.
/// </summary>
public sealed class StatAuditTests
{
    [Fact]
    public void Entries_with_ids_of_their_own_are_not_reported()
    {
        Assert.Empty(StatAudit.Unidentified(new[] { ("goblin", "Goblin"), ("bugbear", "Bugbear") }));
    }

    [Fact]
    public void An_entry_with_no_id_or_a_shared_one_is_reported()
    {
        IReadOnlyList<string> faults = StatAudit.Unidentified(new[] { ("goblin", "Goblin"), (string.Empty, "Bugbear"), ("goblin", "Goblin Boss") });

        Assert.Equal(2, faults.Count);
        Assert.Contains("[1] ('Bugbear') has no nodeId", faults[0], StringComparison.Ordinal);
        Assert.Contains("'goblin' is given to two entries", faults[1], StringComparison.Ordinal);
    }

    [Fact]
    public void A_monster_with_plausible_numbers_is_not_reported()
    {
        StatBlock goblin = Goblin();

        Assert.Empty(StatAudit.Implausible(new[] { goblin }));
        Assert.Empty(StatAudit.WrongChallenge(new[] { goblin }));
        Assert.Empty(StatAudit.EmptyEntries(new[] { goblin }));
    }

    [Theory]
    [InlineData("name", "has no name")]
    [InlineData("ac", "has AC 0")]
    [InlineData("hp", "has 0 hit points")]
    [InlineData("str", "has Str 31")]
    [InlineData("cha", "has Cha 0")]
    public void A_monster_no_monster_could_be_is_reported(string slip, string expected)
    {
        StatBlock monster = slip switch
        {
            "name" => Goblin(name: " "),
            "ac" => Goblin(armourClass: 0),
            "hp" => Goblin(hitPoints: 0),
            "str" => Goblin(abilities: new AbilityScores { Str = 31, Dex = 14, Con = 10, Intelligence = 10, Wis = 8, Cha = 8 }),
            _ => Goblin(abilities: new AbilityScores { Str = 8, Dex = 14, Con = 10, Intelligence = 10, Wis = 8 }),
        };

        string fault = Assert.Single(StatAudit.Implausible(new[] { monster }));

        Assert.Contains(expected, fault, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("1/3", 2, "is no legal one")]
    [InlineData("", 2, "is no legal one")]
    [InlineData("5", 2, "challenge rating 5 gives 3")]
    [InlineData("30", 8, "challenge rating 30 gives 9")]
    public void A_wrong_challenge_rating_or_proficiency_bonus_is_reported(string challenge, int proficiency, string expected)
    {
        string fault = Assert.Single(StatAudit.WrongChallenge(new[] { Goblin(challenge: challenge, proficiency: proficiency) }));

        Assert.Contains(expected, fault, StringComparison.Ordinal);
    }

    [Fact]
    public void An_entry_with_no_text_is_reported()
    {
        StatBlock goblin = Goblin(actions: new[] { new StatBlockEntry { Name = "Scimitar", Text = "Melee Weapon Attack." }, new StatBlockEntry { Name = "Shortbow" } });

        string fault = Assert.Single(StatAudit.EmptyEntries(new[] { goblin }));

        Assert.Contains("'Shortbow' with no text", fault, StringComparison.Ordinal);
    }

    [Fact]
    public void A_spell_cast_by_an_id_the_library_lacks_is_reported_and_one_named_only_is_not()
    {
        var stats = new FakeStatLibrary();
        stats.Spells["sanctuary"] = new SpellEntry { NodeId = "sanctuary", Name = "Sanctuary" };
        StatBlock goblin = Goblin();
        StatBlock caster = new()
        {
            NodeId = "acolyte",
            Name = "Acolyte",
            Spellcasting = new SpellcastingBlock
            {
                Spells = new[]
                {
                    new SpellReference { Name = "Sanctuary", NodeId = "sanctuary" },
                    new SpellReference { Name = "Light" },
                    new SpellReference { Name = "Bless", NodeId = "bles" },
                },
            },
        };

        string fault = Assert.Single(StatAudit.UnextractedSpells(new[] { goblin, caster }, stats));

        Assert.Contains("'acolyte' casts 'Bless' as 'bles'", fault, StringComparison.Ordinal);
    }

    [Fact]
    public void A_source_no_book_gives_a_title_is_reported_once_and_a_blank_one_is_not()
    {
        BookIndex[] books =
        {
            new BookIndex { Book = "srd", Title = "SRD 5.1", Source = "SRD_CC_v5.1.pdf" },
            new BookIndex { Book = "almanac", Title = "A Millbrook Almanac", Source = string.Empty },
            new BookIndex { Book = "untitled", Source = "Untitled.pdf" },
        };

        IReadOnlyList<string> faults = StatAudit.UntitledSources(
            new[] { ("goblin", "SRD_CC_v5.1.pdf"), ("bugbear", "srd_cc_v5.1.pdf "), ("grask", string.Empty), ("hobb", "Homebrew.pdf"), ("nib", "Homebrew.pdf"), ("ghost", "Untitled.pdf") },
            books);

        Assert.Equal(2, faults.Count);
        Assert.Contains("'Homebrew.pdf' is cited by 2 entries ('hobb' first)", faults[0], StringComparison.Ordinal);
        Assert.Contains("'Untitled.pdf'", faults[1], StringComparison.Ordinal);
    }

    private static StatBlock Goblin(
        string name = "Goblin",
        int armourClass = 15,
        int hitPoints = 7,
        AbilityScores abilities = null,
        string challenge = "1/4",
        int proficiency = 2,
        IReadOnlyList<StatBlockEntry> actions = null) => new()
    {
        NodeId = "goblin",
        Name = name,
        ArmourClass = armourClass,
        AverageHitPoints = hitPoints,
        Abilities = abilities ?? new AbilityScores { Str = 8, Dex = 14, Con = 10, Intelligence = 10, Wis = 8, Cha = 8 },
        ChallengeRating = challenge,
        ProficiencyBonus = proficiency,
        Actions = actions ?? new[] { new StatBlockEntry { Name = "Scimitar", Text = "Melee Weapon Attack." } },
    };
}
