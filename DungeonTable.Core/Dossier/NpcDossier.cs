namespace DungeonTable.Core.Dossier;

/// <summary>
/// One non-player character the DM can put in front of the party: who they are, where to find
/// them, what they will say, and what they are good for.
/// </summary>
/// <remarks>
/// <para>
/// Not a stat block. <see cref="Stats.StatBlock"/> already serves the rulebook monsters that get
/// rolled into initiative; this serves the named people a campaign accumulates — a retired player
/// character, a contact, a shopkeeper who owes someone a favour. Most of them will never fight, so
/// the combat fields are optional and <see cref="StatLine"/> is free text rather than a schema. One
/// who does fight <em>uses</em> a stat block (<see cref="StatBlock"/>), the way a level's individuals
/// do, and never is one.
/// </para>
/// <para>
/// <see cref="Knows"/> is the field that earns this type its place. A DM reaching for an NPC
/// mid-session is usually asking "what can this person tell them?", and that answer lives nowhere
/// in a stat block.
/// </para>
/// </remarks>
public sealed class NpcDossier
{
    /// <summary>Stable slug used as the render key ("wenna-brask").</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>The name the party uses ("Wenna Brask").</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>The one-line description ("The miller, Tam's mother").</summary>
    public string Role { get; init; } = string.Empty;

    /// <summary>Where the party can find them ("The mill, by the ford"), or an empty string.</summary>
    public string Where { get; init; } = string.Empty;

    /// <summary>How they stand towards the party ("Ally", "Unknown"), or an empty string.</summary>
    public string Disposition { get; init; } = string.Empty;

    /// <summary>What the party sees, for describing them at the table. Empty when not recorded.</summary>
    public string Appearance { get; init; } = string.Empty;

    /// <summary>Armour class; 0 when the NPC has no stats and does not need any.</summary>
    public int ArmourClass { get; init; }

    /// <summary>Maximum hit points; 0 when not statted.</summary>
    public int MaxHp { get; init; }

    /// <summary>
    /// Whatever else matters mechanically, as one line — saves, senses, resistances, a spell DC, or
    /// a note on which published stat block to reskin. Free text on purpose: most NPCs need a
    /// sentence, not a schema.
    /// </summary>
    public string StatLine { get; init; } = string.Empty;

    /// <summary>
    /// The stat block the NPC fights with, named the way a link names one: the slug of its name
    /// (<c>acolyte</c>). Listed in a room, they join a fight with its numbers, and their card shows it
    /// beneath their own. Empty when they use none; <see cref="ArmourClass"/>, <see cref="MaxHp"/> and
    /// <see cref="StatLine"/> stay the card's numbers at a glance either way.
    /// </summary>
    public string StatBlock { get; init; } = string.Empty;

    /// <summary>Who they are, in prose.</summary>
    public string Summary { get; init; } = string.Empty;

    /// <summary>
    /// What this NPC can tell the party. The reason a DM opens an NPC mid-session.
    /// </summary>
    public IReadOnlyList<DossierBlock> Knows { get; init; } = Array.Empty<DossierBlock>();

    /// <summary>Everything else worth having to hand: what they can do for the party, how to play them.</summary>
    public IReadOnlyList<DossierBlock> Detail { get; init; } = Array.Empty<DossierBlock>();

    /// <summary>The unresolved threads this NPC carries.</summary>
    public IReadOnlyList<DossierBlock> Hooks { get; init; } = Array.Empty<DossierBlock>();

    /// <summary>
    /// The named people this NPC is connected to. Reuses <see cref="CastMember"/>: a contact is the
    /// same shape as a cast row — who, what they are, and where they stand.
    /// </summary>
    public IReadOnlyList<CastMember> Contacts { get; init; } = Array.Empty<CastMember>();
}
