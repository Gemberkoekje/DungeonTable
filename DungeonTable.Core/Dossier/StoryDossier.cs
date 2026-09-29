namespace DungeonTable.Core.Dossier;

/// <summary>
/// The campaign that came before this one: what the players lived through, who they met, and what
/// it left unfinished. Distinct from <see cref="CampaignDossier"/>, which is the published book's
/// front matter — this is the table's own history, and where the two disagree this one wins.
/// </summary>
/// <remarks>
/// <para>
/// A megadungeon campaign that follows an earlier one inherits its debts. The party arrives at the
/// dungeon's door already wealthy, already publicly entangled, already owed favours and already
/// hunted — and the DM running them is usually not the DM who ran that. This is the handoff.
/// </para>
/// <para>
/// A list rather than a single document, so an outgoing DM's prep notes and their session
/// summaries can stay as separate, separately-sourced chapters instead of being merged into one
/// undifferentiated wall.
/// </para>
/// </remarks>
public sealed class StoryDossier
{
    /// <summary>The chapter's display title ("The Harvest Fair - Campaign Handoff").</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Where this came from, for provenance ("finalquest.md, from the outgoing DM").</summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>The one-paragraph version: what happened, and where it leaves the party.</summary>
    public string Summary { get; init; } = string.Empty;

    /// <summary>The cast and the state the handoff leaves each of them in.</summary>
    public IReadOnlyList<CastMember> Cast { get; init; } = Array.Empty<CastMember>();

    /// <summary>The story in sections, in the order it should be read.</summary>
    public IReadOnlyList<DossierBlock> Sections { get; init; } = Array.Empty<DossierBlock>();

    /// <summary>
    /// What the previous campaign left unfinished and this one can use: an errand, an unpaid debt,
    /// a body never found, a name nobody explained.
    /// </summary>
    public IReadOnlyList<DossierBlock> LooseEnds { get; init; } = Array.Empty<DossierBlock>();
}
