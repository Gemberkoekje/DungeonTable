namespace DungeonTable.Web.Services;

/// <summary>What the table recorded of one creature in one area, as <see cref="CampaignState"/> holds it.</summary>
/// <param name="Whole">True when the whole listing was ticked off at once.</param>
/// <param name="Dealt">How many were counted down as dealt with, one at a time.</param>
/// <param name="Fled">How many fled from here.</param>
public readonly record struct CreatureRecord(bool Whole, int Dealt, int Fled)
{
    /// <summary>Nothing recorded: the listing as the book has it.</summary>
    public static CreatureRecord None => default;

    /// <summary>True when anything at all is recorded.</summary>
    public bool Any => Whole || Dealt > 0 || Fled > 0;
}
