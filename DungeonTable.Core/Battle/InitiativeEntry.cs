using System.Linq;

namespace DungeonTable.Core.Battle;

/// <summary>
/// One row in the initiative order. A player or a lone creature has a single member; a monster group
/// ("Bugbears ×4") rolls once and shares this row across all four, which is how 5e is normally run
/// at the table. Splitting a group promotes each member to its own entry.
/// </summary>
/// <remarks>
/// Mutable and owned by <c>BattleState</c>, for the same reasons as <see cref="Combatant"/> — see
/// the remarks there.
/// </remarks>
public sealed class InitiativeEntry
{
    /// <summary>Stable id within the battle.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Insertion order, used as the last tie-break so a sort never shuffles equal rows arbitrarily.
    /// </summary>
    public int Sequence { get; init; }

    /// <summary>The row's initiative, meaningful only while <see cref="HasInitiative"/> is true.</summary>
    public int Initiative { get; set; }

    /// <summary>
    /// False until an initiative has actually been entered or rolled. Blank is a real state at the
    /// table — the DM types the numbers as the players call them out — and it is not the same as a
    /// rolled 0, so unset rows sort to the bottom instead of pretending to be the slowest.
    /// </summary>
    public bool HasInitiative { get; set; }

    /// <summary>The initiative modifier used by the in-app roll, and the first sort tie-break.</summary>
    public int InitiativeModifier { get; set; }

    /// <summary>The row's display label ("Rurik", "Bugbears").</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>The ids of the combatants sharing this row, in display order.</summary>
    public IReadOnlyList<string> MemberIds { get; set; } = Array.Empty<string>();

    /// <summary>What kind of combatants this row holds, so the UI can group and colour it.</summary>
    public CombatantKind Kind { get; init; } = CombatantKind.None;

    /// <summary>
    /// The stat-library node id shared by the row's members, for the group roll's DEX modifier and
    /// the detail pane. Empty for a mixed or unstatted row.
    /// </summary>
    public string StatBlockNodeId { get; init; } = string.Empty;

    /// <summary>Takes a snapshot of this entry, independent of the live one.</summary>
    /// <returns>An independent copy.</returns>
    public InitiativeEntry Copy() => new InitiativeEntry
    {
        Id = Id,
        Sequence = Sequence,
        Initiative = Initiative,
        HasInitiative = HasInitiative,
        InitiativeModifier = InitiativeModifier,
        Label = Label,
        MemberIds = MemberIds.ToArray(),
        Kind = Kind,
        StatBlockNodeId = StatBlockNodeId,
    };
}
