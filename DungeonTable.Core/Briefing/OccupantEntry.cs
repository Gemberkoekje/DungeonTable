namespace DungeonTable.Core.Briefing;

/// <summary>A creature or person the area lists among its creatures.</summary>
public sealed class OccupantEntry
{
    /// <summary>
    /// What to open for it: a stat block's node id, or an entity's or person's id. Empty when the
    /// reference names nothing that is loaded.
    /// </summary>
    public string NodeId { get; init; } = string.Empty;

    /// <summary>Human-readable label; the reference as written when it names nothing.</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>
    /// Node id of the stat block it fights with: the one an individual uses, or, for a stat block
    /// listed as itself, <see cref="NodeId"/>. Empty when there is none.
    /// </summary>
    public string MonsterRef { get; init; } = string.Empty;

    /// <summary>How many, as authored ("4", "1d4+1"); empty when unstated.</summary>
    public string Count { get; init; } = string.Empty;

    /// <summary>The author's note on them here, in prose.</summary>
    public string Note { get; init; } = string.Empty;

    /// <summary>True when the players must not know they are here yet.</summary>
    public bool Secret { get; init; }
}
