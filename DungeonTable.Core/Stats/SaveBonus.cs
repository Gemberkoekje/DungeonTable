namespace DungeonTable.Core.Stats;

/// <summary>A proficient saving throw, as printed in the stat block header ("Con +8").</summary>
public sealed class SaveBonus
{
    /// <summary>The ability the save uses ("Str", "Dex", "Con", "Int", "Wis", "Cha").</summary>
    public string Ability { get; init; } = string.Empty;

    /// <summary>The printed bonus, including proficiency.</summary>
    public int Bonus { get; init; }
}
