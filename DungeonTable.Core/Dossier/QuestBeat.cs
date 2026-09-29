namespace DungeonTable.Core.Dossier;

/// <summary>
/// One step of a quest's story chain — meet the giver, find the thing, hand it back. Beats are the
/// tickable unit in the quest log, so each is a whole action the DM can call done at the table.
/// </summary>
public sealed class QuestBeat
{
    /// <summary>Stable id, unique within the quest; what progress is recorded against.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Position in the chain, counting from 1.</summary>
    public int Ordinal { get; init; }

    /// <summary>The one-line summary shown on the beat row.</summary>
    public string Summary { get; init; } = string.Empty;

    /// <summary>Expanded prose, opened when the DM needs the detail. May be empty.</summary>
    public string Detail { get; init; } = string.Empty;

    /// <summary>
    /// True when the beat is DM-only and must never be read aloud — the same meaning it has
    /// everywhere else in the dossier layer. It earns its keep here: "the acolyte sold the clapper" is
    /// something the DM must know and must not leak.
    /// </summary>
    public bool Secret { get; init; }

    /// <summary>Where the beat happens; an empty target renders no destination chip.</summary>
    public QuestTarget Target { get; init; } = new QuestTarget();
}
