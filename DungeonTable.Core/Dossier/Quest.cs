namespace DungeonTable.Core.Dossier;

/// <summary>
/// One adventure hook: who offers it, what the party is asked to do, the chain of story beats that
/// gets them there, and what it pays. The campaign-wide fourth dossier tier, beside the per-area,
/// per-level and shared-rules ones.
/// </summary>
public sealed class Quest
{
    /// <summary>Stable id, unique across the whole quest log ("the-millers-son").</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>The quest's name as the book prints it ("The Miller's Son").</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Whether it is offered from the outset, gated behind a prerequisite, or found in play.</summary>
    public QuestAvailability Availability { get; init; } = QuestAvailability.None;

    /// <summary>Source page(s) the quest was taken from, for provenance ("8", "8-9").</summary>
    public string Pages { get; init; } = string.Empty;

    /// <summary>Who offers it, as a display label ("Wenna Brask").</summary>
    public string GiverLabel { get; init; } = string.Empty;

    /// <summary>
    /// What the giver's name opens: their id in the NPC roster ("wenna-brask"), so the card is the DM's
    /// own. Empty when none.
    /// </summary>
    public string GiverNodeId { get; init; } = string.Empty;

    /// <summary>The one- or two-sentence pitch, for the collapsed card.</summary>
    public string Hook { get; init; } = string.Empty;

    /// <summary>What must be true before the quest is offered; empty for an unconditional one.</summary>
    public IReadOnlyList<QuestPrerequisite> Prerequisites { get; init; } = Array.Empty<QuestPrerequisite>();

    /// <summary>The story chain, in <see cref="QuestBeat.Ordinal"/> order.</summary>
    public IReadOnlyList<QuestBeat> Beats { get; init; } = Array.Empty<QuestBeat>();

    /// <summary>What completing it pays — coin, favours, renown, friendships.</summary>
    public IReadOnlyList<DossierBlock> Rewards { get; init; } = Array.Empty<DossierBlock>();

    /// <summary>Expanded background: the giver's motive, the lore behind the errand, DM-only truths.</summary>
    public IReadOnlyList<DossierBlock> Detail { get; init; } = Array.Empty<DossierBlock>();
}
