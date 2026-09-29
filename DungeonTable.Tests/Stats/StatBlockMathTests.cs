using DungeonTable.Core.Battle;
using DungeonTable.Core.Stats;
using DungeonTable.Web.Services;

namespace DungeonTable.Tests.Stats;

/// <summary>
/// Verifies the 5e ability-modifier and saving-throw arithmetic <see cref="StatBlockMath"/>
/// centralises, so the combatant detail pane and the reference drawer never disagree on a bonus.
/// </summary>
public sealed class StatBlockMathTests
{
    [Theory]
    [InlineData(1, -5)]
    [InlineData(8, -1)]
    [InlineData(9, -1)]
    [InlineData(10, 0)]
    [InlineData(11, 0)]
    [InlineData(15, 2)]
    [InlineData(20, 5)]
    [InlineData(30, 10)]
    public void Ability_modifier_follows_the_5e_table(int score, int expected) =>
        Assert.Equal(expected, StatBlockMath.AbilityModifier(score));

    [Theory]
    [InlineData(2, "+2")]
    [InlineData(0, "+0")]
    [InlineData(-1, "-1")]
    public void Format_modifier_always_shows_a_sign(int value, string expected) =>
        Assert.Equal(expected, StatBlockMath.FormatModifier(value));

    [Fact]
    public void Score_for_reads_the_named_ability_case_insensitively()
    {
        var abilities = new AbilityScores { Str = 15, Dex = 14, Con = 13, Intelligence = 8, Wis = 11, Cha = 9 };

        Assert.Equal(15, StatBlockMath.ScoreFor(abilities, "Str"));
        Assert.Equal(8, StatBlockMath.ScoreFor(abilities, "int"));
        Assert.Equal(9, StatBlockMath.ScoreFor(abilities, "CHA"));
    }

    [Fact]
    public void Save_for_uses_the_printed_bonus_when_proficient()
    {
        var block = new StatBlock
        {
            Abilities = new AbilityScores { Con = 13, Intelligence = 8 },
            SavingThrows = new[] { new SaveBonus { Ability = "Con", Bonus = 8 }, new SaveBonus { Ability = "Int", Bonus = 9 } },
        };

        Assert.Equal(8, StatBlockMath.SaveFor(block, "Con"));
        Assert.True(StatBlockMath.IsProficientSave(block, "Con"));
    }

    [Fact]
    public void Save_for_derives_from_the_ability_modifier_when_not_proficient()
    {
        var block = new StatBlock
        {
            Abilities = new AbilityScores { Str = 15 },
            SavingThrows = Array.Empty<SaveBonus>(),
        };

        Assert.Equal(2, StatBlockMath.SaveFor(block, "Str"));
        Assert.False(StatBlockMath.IsProficientSave(block, "Str"));
    }

    [Fact]
    public void Slots_spent_reads_the_tally_and_treats_an_untouched_level_as_none()
    {
        var spent = new[] { new SpentSlot { Level = 1, Spent = 2 }, new SpentSlot { Level = 3, Spent = 1 } };

        Assert.Equal(2, StatBlockMath.SlotsSpent(spent, 1));
        Assert.Equal(0, StatBlockMath.SlotsSpent(spent, 2));
        Assert.Equal(1, StatBlockMath.SlotsSpent(spent, 3));
    }

    [Fact]
    public void Slots_spent_on_a_caster_that_has_cast_nothing_is_zero() =>
        Assert.Equal(0, StatBlockMath.SlotsSpent(Array.Empty<SpentSlot>(), 1));
}
