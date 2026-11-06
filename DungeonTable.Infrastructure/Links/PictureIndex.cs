using DungeonTable.Application.Abstractions;
using DungeonTable.Core.Art;
using DungeonTable.Core.Dossier;
using Qowaiv.Validation.Abstractions;

namespace DungeonTable.Infrastructure.Links;

/// <summary>
/// The art catalogue's pictures by what they show: each picture's <see cref="ArtImage.Shows"/> resolved
/// the way a link is (<see cref="LinkTargets"/>), and its older <see cref="ArtImage.NodeIds"/> taken
/// as they are. Built from one reading of the content and one of the catalogue, and immutable after,
/// so it is safe to share; the live facade builds a new one when either changes.
/// </summary>
/// <remarks>
/// <para>
/// Only what an author wrote counts, as with links: a picture titled "Wenna Brask" that does not say it
/// shows <c>wenna-brask</c> is not hers. A name that resolves to nothing, or to a floor with no level
/// file yet, links the picture to nothing, and the content checks name it.
/// </para>
/// <para>
/// Names are resolved when the index is built, not when the picture was written, so a picture that
/// shows <c>ankheg</c> belongs to the book's entry today and moves to the campaign's own stat block by
/// itself the day one is written: the campaign's content wins over the book index, as for a link.
/// </para>
/// <para>
/// A creature a level declares (a band of goblins, the Bell-Warden) that no picture shows has its stat
/// block's pictures instead: the author said what it is, and a goblin's picture is what the table needs
/// when the band comes round the corner. A person, or an entity that is not a creature, gets none: a
/// stranger's face would be worse than no picture.
/// </para>
/// </remarks>
public sealed class PictureIndex
{
    private static readonly IReadOnlyList<ArtImage> None = Array.Empty<ArtImage>();

    private readonly Dictionary<string, IReadOnlyList<ArtImage>> byTarget;

    private PictureIndex(Dictionary<string, IReadOnlyList<ArtImage>> byTarget)
    {
        this.byTarget = byTarget;
    }

    /// <summary>Indexes pictures by what they show.</summary>
    /// <param name="images">The catalogue's pictures, in the order a tie between two should break.</param>
    /// <param name="targets">Everything a link can name in the content showing now.</param>
    /// <param name="dossiers">The content those were built from, for the stat block each creature a level declares uses.</param>
    /// <returns>The index.</returns>
    public static PictureIndex Build(IEnumerable<ArtImage> images, LinkTargets targets, IDossierStore dossiers)
    {
        ArgumentNullException.ThrowIfNull(images);
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(dossiers);

        var found = new Dictionary<string, List<Placing>>(StringComparer.Ordinal);
        int order = 0;
        foreach (ArtImage image in images.Where(image => image is not null))
        {
            IReadOnlyList<string> shown = TargetsOf(image, targets);
            for (int position = 0; position < shown.Count; position++)
            {
                if (!found.TryGetValue(shown[position], out List<Placing> placings))
                {
                    placings = new List<Placing>();
                    found[shown[position]] = placings;
                }

                placings.Add(new Placing(image, IsScene(image), position, shown.Count, order));
            }

            order++;
        }

        var byTarget = new Dictionary<string, IReadOnlyList<ArtImage>>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, List<Placing>> target in found)
        {
            byTarget[target.Key] = target.Value
                .OrderBy(placing => placing.Scene)
                .ThenBy(placing => placing.Position)
                .ThenBy(placing => placing.Shows)
                .ThenBy(placing => placing.Order)
                .Select(placing => placing.Image)
                .ToArray();
        }

        foreach ((string creature, string statBlock) in StandIns(targets, dossiers))
        {
            if (!byTarget.ContainsKey(creature) && byTarget.TryGetValue(statBlock, out IReadOnlyList<ArtImage> pictures))
            {
                byTarget[creature] = pictures;
            }
        }

        return new PictureIndex(byTarget);
    }

    /// <summary>
    /// The ids a picture shows, in its own order and each once: what its <see cref="ArtImage.Shows"/>
    /// resolve to, then its <see cref="ArtImage.NodeIds"/>. A name that resolves to nothing is left out.
    /// </summary>
    /// <param name="image">The picture.</param>
    /// <param name="targets">Everything a link can name.</param>
    /// <returns>The ids (possibly empty).</returns>
    public static IReadOnlyList<string> TargetsOf(ArtImage image, LinkTargets targets)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(targets);

        var ids = new List<string>();
        foreach (string name in image.Shows ?? Array.Empty<string>())
        {
            // A picture belongs to no floor, so an area is named with its floor, as in a quest. One on a
            // floor with no level file yet resolves with no id, and is dropped below with the blanks.
            Result<LinkTarget> resolved = targets.Resolve(name, string.Empty);
            if (resolved.IsValid)
            {
                ids.Add(resolved.Value.TargetId);
            }
        }

        ids.AddRange(image.NodeIds ?? Array.Empty<string>());
        return ids
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>The pictures that show a thing, best first (see <see cref="IPictureLinks.PicturesOf"/>).</summary>
    /// <param name="targetId">The id a resolved link carries.</param>
    /// <returns>The pictures (possibly empty).</returns>
    public IReadOnlyList<ArtImage> PicturesOf(string targetId) =>
        byTarget.TryGetValue(targetId ?? string.Empty, out IReadOnlyList<ArtImage> images) ? images : None;

    // A picture of a place or a prop, or one the DM hands round, rather than of somebody: a portrait of a
    // thing comes before a scene it is in (the manticore before the chapter's opening spread).
    private static bool IsScene(ArtImage image) =>
        image.Kind is ArtKind.Location or ArtKind.Handout or ArtKind.Diagram;

    // Each creature a level declares, with the node id of the stat block it uses, read on its own floor.
    private static IEnumerable<(string Creature, string StatBlock)> StandIns(LinkTargets targets, IDossierStore dossiers)
    {
        foreach (LevelDossier level in dossiers.AllLevels())
        {
            foreach (Entity entity in level.AllEntities().Where(entity => entity.Kind == EntityKind.Creature && !string.IsNullOrWhiteSpace(entity.StatBlock)))
            {
                Result<LinkTarget> block = targets.Resolve(entity.StatBlock, level.LevelNodeId);
                if (block.IsValid && block.Value.Kind == CrossRefKind.Monster && !string.IsNullOrEmpty(entity.Id))
                {
                    yield return (entity.Id, block.Value.TargetId);
                }
            }
        }
    }

    // One picture among those that show a thing: whether it is a scene, how early it names it, how many
    // things it shows, and where it stands in the catalogue.
    private sealed record Placing(ArtImage Image, bool Scene, int Position, int Shows, int Order);
}
