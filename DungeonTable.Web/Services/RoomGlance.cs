using System.Collections.Generic;
using System.Linq;
using DungeonTable.Core.Briefing;
using DungeonTable.Core.Dossier;

namespace DungeonTable.Web.Services;

/// <summary>
/// What the DM can tell about a room without opening it: who the area lists, what is to be found
/// there, the checks it calls for, the traps, the secret ways, and where it leads. The badges beside the
/// room's label on the DM's map, its peek card, and its line in a neighbour's Nearby list are all read
/// from this.
/// </summary>
/// <remarks>
/// Everything comes from what the content already holds (the area's creatures, its blocks tagged loot,
/// its checks and exits) and what the Room Editor marked on the map (<see cref="RoomMarks"/>).
/// Nothing is read out of the prose. What changes during the evening (which creatures are ticked off,
/// the DM's note) is not here but in <see cref="RoomBadges"/>, worked out at each render. The creatures
/// the DM moved into the room are added to <see cref="Creatures"/> each time the glance is asked for
/// (<see cref="WithCreatures"/>), never kept in it.
/// </remarks>
public sealed class RoomGlance
{
    /// <summary>A room with nothing to show: no dossier is authored for it.</summary>
    public static RoomGlance None { get; } = new RoomGlance();

    /// <summary>The area's node id; empty for <see cref="None"/>.</summary>
    public string AreaNodeId { get; init; } = string.Empty;

    /// <summary>The area's title, as the Room tab shows it.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>The area's key number as printed on the map; 0 when it has none.</summary>
    public int AreaNumber { get; init; }

    /// <summary>The creatures the area lists, one row each, as the encounter strip lists them.</summary>
    public IReadOnlyList<EncounterMonster> Creatures { get; init; } = Array.Empty<EncounterMonster>();

    /// <summary>The headings of the area's blocks tagged loot, at a glance and in the detail.</summary>
    public IReadOnlyList<string> Loot { get; init; } = Array.Empty<string>();

    /// <summary>The ability checks the area calls for.</summary>
    public IReadOnlyList<SkillCheck> Checks { get; init; } = Array.Empty<SkillCheck>();

    /// <summary>The traps and hazards the map marks in the room.</summary>
    public IReadOnlyList<string> Hazards { get; init; } = Array.Empty<string>();

    /// <summary>
    /// The room's secret ways, each as a line: the secret doors in its walls and the secret or hidden-door
    /// markers the map shows in it, and its exits the players do not know of.
    /// </summary>
    /// <remarks>
    /// A block marked secret is not one of them. On a real floor nearly every room has one, since that
    /// flag means "DM only, never read aloud" (on one level, 72 rooms of 89), so a badge for it would be on
    /// every room and say nothing. A hidden way is rarer and is what the DM plans around. A creature listed
    /// as secret is not one either: it is in the creature badge and marked on the peek card.
    /// </remarks>
    public IReadOnlyList<string> Secrets { get; init; } = Array.Empty<string>();

    /// <summary>Where the room leads: the area's exits, as the briefing resolves them.</summary>
    public IReadOnlyList<ConnectionEntry> Exits { get; init; } = Array.Empty<ConnectionEntry>();

    /// <summary>True for <see cref="None"/>: there is no room to show.</summary>
    public bool IsEmpty => AreaNodeId.Length == 0;

    /// <summary>True when the room has a secret way: a secret door in its walls, a marker for one, a secret exit.</summary>
    public bool HasSecret => Secrets.Count > 0;

    /// <summary>Puts a room's glance together from its dossier, its briefing, its encounter and the map's marks.</summary>
    /// <param name="dossier">The area's dossier.</param>
    /// <param name="briefing">The area's briefing, for its resolved exits; may be empty.</param>
    /// <param name="creatures">The area's encounter rows.</param>
    /// <param name="marks">What the map marks in the room.</param>
    /// <returns>The glance; <see cref="None"/> when the dossier names no area.</returns>
    public static RoomGlance From(
        AreaDossier dossier,
        RoomBriefing briefing,
        IReadOnlyList<EncounterMonster> creatures,
        RoomMarks marks)
    {
        if (dossier is null || string.IsNullOrEmpty(dossier.AreaNodeId))
        {
            return None;
        }

        DossierBlock[] blocks = dossier.Glance.Concat(dossier.Detail).Where(block => block is not null).ToArray();
        IReadOnlyList<ConnectionEntry> exits = briefing?.Connections ?? Array.Empty<ConnectionEntry>();
        RoomMarks marked = marks ?? RoomMarks.None;

        return new RoomGlance
        {
            AreaNodeId = dossier.AreaNodeId,
            Title = dossier.Title,
            AreaNumber = dossier.AreaNumber,
            Creatures = creatures ?? Array.Empty<EncounterMonster>(),
            Loot = Headings(blocks.Where(block => block.Kind == DossierBlockKind.Loot)),
            Checks = dossier.SkillChecks,
            Hazards = marked.Hazards,
            Secrets = marked.Secrets
                .Concat(exits.Where(exit => exit is not null && exit.Secret).Select(exit => "Secret way to " + exit.Label))
                .ToArray(),
            Exits = exits,
        };
    }

    /// <summary>The same glance with other creatures: the ones the area lists, and the ones moved here.</summary>
    /// <param name="creatures">The rows to show.</param>
    /// <returns>A copy of this glance with those creatures.</returns>
    public RoomGlance WithCreatures(IReadOnlyList<EncounterMonster> creatures) => new RoomGlance
    {
        AreaNodeId = AreaNodeId,
        Title = Title,
        AreaNumber = AreaNumber,
        Creatures = creatures ?? Array.Empty<EncounterMonster>(),
        Loot = Loot,
        Checks = Checks,
        Hazards = Hazards,
        Secrets = Secrets,
        Exits = Exits,
    };

    private static string[] Headings(IEnumerable<DossierBlock> blocks) =>
        blocks
            .Select(block => block.Heading.Trim())
            .Where(heading => heading.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
}
