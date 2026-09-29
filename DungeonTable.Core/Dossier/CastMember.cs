namespace DungeonTable.Core.Dossier;

/// <summary>
/// One row of a <see cref="StoryDossier"/>'s cast table: a person from the campaign that came
/// before, what they were to the party, and what state they were left in.
/// </summary>
/// <remarks>
/// The state matters more than the role. A DM reaching for this table mid-session is almost always
/// answering "wait, is that one still alive?" about a name a player just said.
/// </remarks>
public sealed class CastMember
{
    /// <summary>The name as the players know it ("Wenna Brask").</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>What they were to the party ("Ally. Fixer, political cover, safe house.").</summary>
    public string Role { get; init; } = string.Empty;

    /// <summary>Where the handoff leaves them ("Dead - killed by the Steward.").</summary>
    public string State { get; init; } = string.Empty;

    /// <summary>
    /// True when what this row records is not something the players know. The panel styles these
    /// as DM-only, the same as a secret dossier block.
    /// </summary>
    public bool Secret { get; init; }

    /// <summary>
    /// The person's id when they have a card of their own ("tam-brask"), so the row opens
    /// it. Empty for someone who has none. As an NPC's contact it is the far end of a relation.
    /// </summary>
    public string Ref { get; init; } = string.Empty;

    /// <summary>
    /// On an NPC's contact that has a <see cref="Ref"/>: the relation it records, from the NPC to the
    /// contact ("family", "protects"), one of <see cref="RelationKinds.Authored"/>. Empty for a
    /// contact that is only a name on a card.
    /// </summary>
    public string Rel { get; init; } = string.Empty;
}
