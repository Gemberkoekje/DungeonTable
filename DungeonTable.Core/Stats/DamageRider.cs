namespace DungeonTable.Core.Stats;

/// <summary>
/// Extra damage tacked onto an attack ("plus 7 (2d6) poison damage"), parsed best-effort from an
/// <see cref="AttackLine"/>'s original text.
/// </summary>
public sealed class DamageRider
{
    /// <summary>The dice expression ("2d6").</summary>
    public string DamageDice { get; init; } = string.Empty;

    /// <summary>The average of <see cref="DamageDice"/>, as printed.</summary>
    public int DamageAverage { get; init; }

    /// <summary>The damage type ("poison").</summary>
    public string DamageType { get; init; } = string.Empty;
}
