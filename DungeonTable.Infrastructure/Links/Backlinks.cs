using DungeonTable.Application.Abstractions;
using DungeonTable.Core.Dossier;
using Qowaiv.Validation.Abstractions;

namespace DungeonTable.Infrastructure.Links;

/// <summary>
/// Where each thing a link can name turns up in the authored areas: the rooms whose creature list
/// names it, and the rooms whose prose links to it. It is what a card's "appears in" reads, so the
/// card can send the DM somewhere. Built once from the loaded
/// content and immutable afterwards, so it is safe to share as a singleton.
/// </summary>
/// <remarks>
/// Only what an author wrote counts: a creature entry or a <c>[[…]]</c> link. A name that merely
/// turns up in unmarked prose is not an appearance, for the reason links are authored at all: a
/// mention in text is not a relationship.
/// </remarks>
public sealed class Backlinks
{
    /// <summary>How an area that lists something among its creatures is described.</summary>
    public const string InTheRoom = "in the room";

    /// <summary>How an area whose prose links to something is described.</summary>
    public const string Mentioned = "mentioned";

    private readonly Dictionary<string, List<AreaLink>> byTarget;

    private Backlinks(Dictionary<string, List<AreaLink>> byTarget)
    {
        this.byTarget = byTarget;
    }

    /// <summary>Reads every authored area's creatures and links.</summary>
    /// <param name="dossiers">Supplies the areas and the floor each one is on.</param>
    /// <param name="targets">Resolves what each creature entry and link names.</param>
    /// <returns>The index.</returns>
    public static Backlinks Build(IDossierStore dossiers, LinkTargets targets)
    {
        ArgumentNullException.ThrowIfNull(dossiers);
        ArgumentNullException.ThrowIfNull(targets);

        var found = new Dictionary<string, List<(AreaDossier Area, string How)>>(StringComparer.Ordinal);
        foreach (AreaDossier area in dossiers.AllAreas())
        {
            Result<LevelDossier> level = dossiers.GetLevelForArea(area.AreaNodeId);
            string floor = level.IsValid ? level.Value.LevelNodeId : string.Empty;

            // Only a creature entry that names a creature: one naming a faction is broken, and the
            // briefing shows it so.
            foreach (AreaCreature creature in area.Creatures.Where(creature => creature is not null))
            {
                Result<LinkTarget> resolved = targets.Resolve(creature.Ref, floor);
                if (resolved.IsValid && resolved.Value.CanBeInARoom)
                {
                    Record(found, resolved, area, InTheRoom);
                }
            }

            foreach (string text in ProseOf(area))
            {
                foreach (MarkedLink link in LinkMarkup.Parse(text))
                {
                    Record(found, targets.Resolve(link.Target, floor), area, Mentioned);
                }
            }
        }

        var byTarget = new Dictionary<string, List<AreaLink>>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, List<(AreaDossier Area, string How)>> pair in found)
        {
            byTarget[pair.Key] = pair.Value
                .GroupBy(hit => hit.Area.AreaNodeId, StringComparer.Ordinal)
                .Select(hits => new AreaLink
                {
                    NodeId = hits.Key,
                    Label = hits.First().Area.Title,
                    Relation = string.Join(", ", hits.Select(hit => hit.How).Distinct(StringComparer.Ordinal)),
                })
                .ToList();
        }

        return new Backlinks(byTarget);
    }

    /// <summary>
    /// The areas a target appears in, in the order the areas were loaded, each once, with how:
    /// <see cref="InTheRoom"/>, <see cref="Mentioned"/>, or both.
    /// </summary>
    /// <param name="targetId">The id a link resolves to: an entity's, a person's, or a stat block's node id.</param>
    /// <returns>The areas (possibly empty).</returns>
    public IReadOnlyList<AreaLink> For(string targetId) =>
        !string.IsNullOrEmpty(targetId) && byTarget.TryGetValue(targetId, out List<AreaLink> areas)
            ? areas
            : Array.Empty<AreaLink>();

    // Every field of an area that the app renders through the link resolver, and so every place an
    // author can have written a link: the same fields the content rules check.
    private static IEnumerable<string> ProseOf(AreaDossier area)
    {
        yield return area.ReadAloud;

        foreach (DossierBlock block in area.Glance.Concat(area.Detail).Where(block => block is not null))
        {
            yield return block.Body;
        }

        foreach (AreaCreature creature in area.Creatures.Where(creature => creature is not null))
        {
            yield return creature.Note;
        }

        foreach (AreaExit exit in area.Exits.Where(exit => exit is not null))
        {
            yield return exit.Note;
        }
    }

    private static void Record(
        Dictionary<string, List<(AreaDossier Area, string How)>> found,
        Result<LinkTarget> resolved,
        AreaDossier area,
        string how)
    {
        // A broken link, or one to a floor nobody has authored, names nothing to list it under.
        if (!resolved.IsValid || resolved.Value.TargetId.Length == 0)
        {
            return;
        }

        if (!found.TryGetValue(resolved.Value.TargetId, out List<(AreaDossier Area, string How)> hits))
        {
            hits = new List<(AreaDossier Area, string How)>();
            found[resolved.Value.TargetId] = hits;
        }

        hits.Add((area, how));
    }
}
