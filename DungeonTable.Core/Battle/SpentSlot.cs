namespace DungeonTable.Core.Battle;

/// <summary>
/// How many spell slots of one level a combatant has burned this fight, so "how many
/// <em>magic missiles</em> has it got left" is answerable without the DM keeping tallies on paper.
/// The <em>total</em> it counts against lives on the creature's
/// <see cref="Stats.SpellcastingBlock.Slots"/>; this only ever records expenditure.
/// </summary>
public sealed class SpentSlot
{
    /// <summary>The spell level (1-9).</summary>
    public int Level { get; init; }

    /// <summary>How many slots of that level have been spent.</summary>
    public int Spent { get; init; }
}
