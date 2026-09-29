namespace DungeonTable.Core.Stats;

/// <summary>A full spell entry, extracted from the rulebooks and keyed by node id ("data_statblocks_spells_dispel_magic").</summary>
public sealed class SpellEntry
{
    /// <summary>The node id this entry is keyed to ("data_statblocks_spells_dispel_magic").</summary>
    public string NodeId { get; init; } = string.Empty;

    /// <summary>The spell's name ("Fireball").</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Spell level (0 = cantrip).</summary>
    public int Level { get; init; }

    /// <summary>The magic school ("evocation").</summary>
    public string School { get; init; } = string.Empty;

    /// <summary>Casting time as printed ("1 action").</summary>
    public string CastingTime { get; init; } = string.Empty;

    /// <summary>Range as printed ("150 feet").</summary>
    public string Range { get; init; } = string.Empty;

    /// <summary>Components as printed ("V, S, M (a tiny ball of bat guano and sulfur)").</summary>
    public string Components { get; init; } = string.Empty;

    /// <summary>Duration as printed ("Instantaneous", "Concentration, up to 1 minute").</summary>
    public string Duration { get; init; } = string.Empty;

    /// <summary>True when the duration requires concentration.</summary>
    public bool Concentration { get; init; }

    /// <summary>True when the spell can be cast as a ritual.</summary>
    public bool Ritual { get; init; }

    /// <summary>The full spell description.</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>The "At Higher Levels" text, or empty when the spell has none.</summary>
    public string HigherLevels { get; init; } = string.Empty;

    /// <summary>The source book this entry was extracted from ("SRD_CC_v5.1.pdf").</summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>The location within <see cref="Source"/> ("p.144").</summary>
    public string SourceLocation { get; init; } = string.Empty;
}
