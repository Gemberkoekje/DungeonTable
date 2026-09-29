using DungeonTable.Web.Services;

namespace DungeonTable.Tests.Battles;

/// <summary>
/// Verifies the count reader takes the number an author wrote in an area's creature list, keeps a
/// dice expression for the DM to roll, and pre-fills one for anything it cannot read.
/// </summary>
public sealed class EncounterCountTests
{
    [Theory]
    [InlineData("4", 4)]
    [InlineData(" 12 ", 12)]
    [InlineData("99", 99)]
    public void An_authored_number_is_the_count(string written, int expected)
    {
        Assert.True(EncounterCount.TryReadAuthored(written, out CountEstimate estimate));

        Assert.Equal(new CountEstimate(expected, string.Empty), estimate);
    }

    [Theory]
    [InlineData("1d4+1", "1d4+1")]
    [InlineData("2d6", "2d6")]
    [InlineData("1d4  +  1", "1d4 + 1")]
    public void An_authored_dice_expression_is_kept_for_the_dm_to_roll(string written, string expression)
    {
        Assert.True(EncounterCount.TryReadAuthored(written, out CountEstimate estimate));

        Assert.Equal(new CountEstimate(1, expression), estimate);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void No_authored_count_means_one(string written)
    {
        Assert.True(EncounterCount.TryReadAuthored(written, out CountEstimate estimate));

        Assert.Equal(CountEstimate.One, estimate);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("100")]
    [InlineData("-2")]
    [InlineData("four")]
    [InlineData("a few")]
    [InlineData("1.5")]
    [InlineData("4 bandits")]
    public void Anything_else_is_not_a_count_and_pre_fills_one(string written)
    {
        Assert.False(EncounterCount.TryReadAuthored(written, out CountEstimate estimate));

        Assert.Equal(CountEstimate.One, estimate);
    }
}
