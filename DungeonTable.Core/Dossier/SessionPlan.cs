namespace DungeonTable.Core.Dossier;

/// <summary>
/// One session's prep: the facts it rests on, the scenes in the order they should run, and the
/// decisions the DM still has to make before running it.
/// </summary>
/// <remarks>
/// <para>
/// The tier the other dossiers leave homeless. A person belongs on the NPCs tab and a hook belongs
/// on the Quests tab, but a running order does not belong to either — it is a statement about the
/// evening, not about the campaign, and it stops being true the moment the session is played.
/// </para>
/// <para>
/// <see cref="Decisions"/> is deliberately part of the document rather than a comment in it. Prep
/// for a session that follows someone else's campaign is mostly a list of things nobody has
/// settled yet, and a plan that hides them reads as finished when it is not.
/// </para>
/// </remarks>
public sealed class SessionPlan
{
    /// <summary>Stable slug used as the render key ("01-the-miller-at-dusk").</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>The session's display title ("Session 1 - The Miller at Dusk").</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Where the prep came from, for provenance ("Sessions/01-the-miller-at-dusk.md").</summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>What the session is, in a paragraph.</summary>
    public string Summary { get; init; } = string.Empty;

    /// <summary>
    /// The facts the plan rests on that are settled rather than suggested, so a beat that
    /// contradicts one is visibly a change rather than a drift.
    /// </summary>
    public IReadOnlyList<DossierBlock> Canon { get; init; } = Array.Empty<DossierBlock>();

    /// <summary>The scenes, in the order they should run.</summary>
    public IReadOnlyList<SessionScene> Scenes { get; init; } = Array.Empty<SessionScene>();

    /// <summary>What the DM still has to decide before running it.</summary>
    public IReadOnlyList<DossierBlock> Decisions { get; init; } = Array.Empty<DossierBlock>();
}
