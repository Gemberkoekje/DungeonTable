namespace DungeonTable.Core.Stats;

/// <summary>The number of spell slots a caster has at one spell level.</summary>
public sealed class SpellSlotLevel
{
    /// <summary>The spell level (1-9).</summary>
    public int Level { get; init; }

    /// <summary>Total slots of this level.</summary>
    public int Total { get; init; }
}
