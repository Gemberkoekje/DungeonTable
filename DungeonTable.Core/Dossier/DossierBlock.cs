namespace DungeonTable.Core.Dossier;

/// <summary>
/// One titled block of a dossier: a boldface at-a-glance bullet, an expanded detail subsection,
/// a faction write-up, or a shared rules section. The heading is the scannable unit; the body is
/// the prose shown when the block is expanded.
/// </summary>
public sealed class DossierBlock
{
    /// <summary>
    /// A slug that makes a level's faction block that faction's entity ("wickfoot-goblins"), so prose
    /// can link to it and a card can describe it (see <see cref="LevelDossier.AllEntities"/>). Empty on
    /// every other block.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Short heading ("Sandy Floor", "ONE-WAY SECRET DOOR", "Doors and Secret Doors").</summary>
    public string Heading { get; init; } = string.Empty;

    /// <summary>The block's prose body.</summary>
    public string Body { get; init; } = string.Empty;

    /// <summary>
    /// True when the block is DM-only and must never be read aloud to the players (a secret door,
    /// a hidden ambush, a concealed message). The panel styles these distinctly.
    /// </summary>
    public bool Secret { get; init; }

    /// <summary>The block's classification, for styling and ordering.</summary>
    public DossierBlockKind Kind { get; init; } = DossierBlockKind.None;

    /// <summary>Clickable cross-references found in the body.</summary>
    public IReadOnlyList<CrossRef> CrossRefs { get; init; } = Array.Empty<CrossRef>();
}
