namespace DungeonTable.Core.Stats;

/// <summary>A spell named in a <see cref="SpellcastingBlock"/>'s prepared/known list.</summary>
public sealed class SpellReference
{
    /// <summary>The spell level (0 = cantrip / at-will).</summary>
    public int Level { get; init; }

    /// <summary>The spell's name ("Fireball").</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>The library node id for the full <see cref="SpellEntry"/> ("data_statblocks_spells_dispel_magic").</summary>
    public string NodeId { get; init; } = string.Empty;

    /// <summary>Uses per day for an innate/limited spell ("3/day"), or empty when unlimited.</summary>
    public string PerDay { get; init; } = string.Empty;
}
