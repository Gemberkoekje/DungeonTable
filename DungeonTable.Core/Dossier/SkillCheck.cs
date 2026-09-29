namespace DungeonTable.Core.Dossier;

/// <summary>
/// An ability-check callout parsed from a dossier ("a successful DC 20 Wisdom (Perception)
/// check"), surfaced by the DM panel as a compact reference pill.
/// </summary>
public sealed class SkillCheck
{
    /// <summary>The ability and skill, as written ("Wisdom (Perception)", "Strength (Athletics)").</summary>
    public string Ability { get; init; } = string.Empty;

    /// <summary>The difficulty class.</summary>
    public int Dc { get; init; }

    /// <summary>What the check accomplishes ("hear the bandit's retreating footfalls").</summary>
    public string Purpose { get; init; } = string.Empty;
}
