namespace DungeonTable.Core.Dossier;

/// <summary>
/// One scene of a <see cref="SessionPlan"/>: where it happens, who is on, roughly how long it
/// should take, and the beats to hit while it runs.
/// </summary>
/// <remarks>
/// <see cref="Cast"/> is the field that earns the type its place. A session's running order is
/// mostly a pacing decision, and the pacing question is always "who is sitting and watching while
/// this happens?" — a scene that lists one player character is a scene the rest of the table is
/// spectating.
/// </remarks>
public sealed class SessionScene
{
    /// <summary>Stable slug used as the render key ("cold-open-at-the-mill").</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Position in the running order, from 0. Shown as the scene's badge.</summary>
    public int Ordinal { get; init; }

    /// <summary>Where it happens ("The mill at dusk").</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>When it happens and roughly how long to give it ("Late afternoon, about 5 minutes").</summary>
    public string When { get; init; } = string.Empty;

    /// <summary>Which player characters are on, as one line ("Maren, Oswin").</summary>
    public string Cast { get; init; } = string.Empty;

    /// <summary>Why the scene is in the running order at all, in one line.</summary>
    public string Purpose { get; init; } = string.Empty;

    /// <summary>The beats to hit while it runs, in the order they should land.</summary>
    public IReadOnlyList<DossierBlock> Beats { get; init; } = Array.Empty<DossierBlock>();
}
