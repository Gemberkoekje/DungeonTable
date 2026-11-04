using DungeonTable.Web.Services;

namespace DungeonTable.Tests.Web;

/// <summary>
/// The count on a room's creature badge, and the names under a neighbouring room in Nearby: plain counts
/// add up, a count rolled at the table is kept as written, and its constant joins the rest.
/// </summary>
public sealed class CreatureTallyTests
{
    [Fact]
    public void Plain_counts_add_up()
    {
        Assert.Equal("4", CreatureTally.Count(new[] { Row("Grask"), Row("Bugbear", 2), Row("Tam Brask") }));
    }

    [Fact]
    public void A_rolled_count_is_kept_as_written_and_the_rest_join_its_constant()
    {
        // Nib and 1d4+2 goblins: the badge says 1d4+3, not a guess.
        Assert.Equal("1d4+3", CreatureTally.Count(new[] { Row("Nib"), Rolled("Goblin", "1d4+2") }));
    }

    [Fact]
    public void Dice_of_one_size_add_up_and_other_sizes_follow_in_the_order_they_came()
    {
        EncounterMonster[] rows = { Rolled("Skeleton", "1d6"), Rolled("Rat", "2d4 - 1"), Rolled("Zombie", "1d6+1"), Row("Nib") };

        Assert.Equal("2d6+2d4+1", CreatureTally.Count(rows));
    }

    [Fact]
    public void A_constant_that_comes_to_less_than_nothing_is_kept_and_one_that_comes_to_nothing_is_left_off()
    {
        Assert.Equal("1d4-1", CreatureTally.Count(new[] { Rolled("Rat", "1d4-1") }));
        Assert.Equal("1d4", CreatureTally.Count(new[] { Rolled("Rat", "1d4-1"), Row("Nib") }));
    }

    [Fact]
    public void No_rows_count_nothing()
    {
        Assert.Equal(string.Empty, CreatureTally.Count(Array.Empty<EncounterMonster>()));
    }

    [Fact]
    public void Names_are_listed_in_order_with_a_count_before_each_that_is_not_one()
    {
        EncounterMonster[] rows = { Row("Grask"), Row("Bugbear", 2), Rolled("Goblin", "1d4+2"), Row("Tam Brask") };

        Assert.Equal("Grask, 2 × Bugbear, 1d4+2 × Goblin, Tam Brask", CreatureTally.Names(rows));
    }

    private static EncounterMonster Row(string name, int count = 1) =>
        new EncounterMonster { NodeId = name.ToLowerInvariant(), Name = name, SuggestedCount = count };

    private static EncounterMonster Rolled(string name, string dice) =>
        new EncounterMonster { NodeId = name.ToLowerInvariant(), Name = name, SuggestedCount = 1, CountExpression = dice };
}
