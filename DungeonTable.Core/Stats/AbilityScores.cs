namespace DungeonTable.Core.Stats;

/// <summary>The six ability scores of a stat block, as printed (not modifiers).</summary>
public sealed class AbilityScores
{
    /// <summary>Strength score.</summary>
    public int Str { get; init; }

    /// <summary>Dexterity score.</summary>
    public int Dex { get; init; }

    /// <summary>Constitution score.</summary>
    public int Con { get; init; }

    /// <summary>Intelligence score.</summary>
    public int Intelligence { get; init; }

    /// <summary>Wisdom score.</summary>
    public int Wis { get; init; }

    /// <summary>Charisma score.</summary>
    public int Cha { get; init; }
}
