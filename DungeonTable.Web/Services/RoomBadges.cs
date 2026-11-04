using System.Collections.Generic;
using System.Linq;

namespace DungeonTable.Web.Services;

/// <summary>
/// The badges beside a room's label on the DM's map, as they stand now: the creatures not yet ticked
/// off, cleared, loot, a trap or hazard, a secret way, and the DM's note. The room's
/// <see cref="RoomGlance"/> says what is there; the table's checklist and notes say what has happened,
/// so these are worked out at each render, never kept.
/// </summary>
public sealed class RoomBadges
{
    // Each glyph is followed by U+FE0E, which asks for the plain text glyph rather than an emoji, so a
    // badge keeps the colour it is drawn in.
    private const string SkullGlyph = "☠\uFE0E";
    private const string TickGlyph = "✔\uFE0E";
    private const string LootGlyph = "◆";
    private const string HazardGlyph = "⚠\uFE0E";
    private const string SecretGlyph = "S";
    private const string NoteGlyph = "✎\uFE0E";

    /// <summary>No badges: a room with nothing to show.</summary>
    public static RoomBadges None { get; } = new RoomBadges();

    /// <summary>The creatures the area lists that are not ticked off yet, in its order.</summary>
    public IReadOnlyList<EncounterMonster> StillUp { get; init; } = Array.Empty<EncounterMonster>();

    /// <summary>
    /// How many creatures are still up, as the badge shows it ("3", "1d4+3"); empty when the area lists
    /// none or every one is ticked off.
    /// </summary>
    public string Creatures { get; init; } = string.Empty;

    /// <summary>True when the area lists creatures and every one of them is ticked off.</summary>
    public bool Cleared { get; init; }

    /// <summary>True when the area has a block tagged loot.</summary>
    public bool Loot { get; init; }

    /// <summary>True when the map marks a trap or a hazard in the room.</summary>
    public bool Hazard { get; init; }

    /// <summary>True when the room has a secret way (see <see cref="RoomGlance.Secrets"/>).</summary>
    public bool Secret { get; init; }

    /// <summary>The DM's note on the room; empty when none is written.</summary>
    public string Note { get; init; } = string.Empty;

    /// <summary>True when there is no badge to draw.</summary>
    public bool IsEmpty => Parts.Count == 0;

    /// <summary>The badges to draw, in order: creatures or cleared, loot, trap or hazard, secret, note.</summary>
    public IReadOnlyList<BadgePart> Parts
    {
        get
        {
            var parts = new List<BadgePart>(6);
            if (Creatures.Length > 0)
            {
                parts.Add(new BadgePart(BadgeKind.Creatures, SkullGlyph + " " + Creatures, CreaturesTitle()));
            }
            else if (Cleared)
            {
                parts.Add(new BadgePart(BadgeKind.Cleared, TickGlyph, "Cleared: every creature here is ticked off"));
            }

            if (Loot)
            {
                parts.Add(new BadgePart(BadgeKind.Loot, LootGlyph, "Loot"));
            }

            if (Hazard)
            {
                parts.Add(new BadgePart(BadgeKind.Hazard, HazardGlyph, "A trap or hazard"));
            }

            if (Secret)
            {
                parts.Add(new BadgePart(BadgeKind.Secret, SecretGlyph, "A secret way: a secret door or a hidden exit"));
            }

            if (Note.Length > 0)
            {
                parts.Add(new BadgePart(BadgeKind.Note, NoteGlyph, "You wrote a note"));
            }

            return parts;
        }
    }

    /// <summary>The badges of a room now: its glance, read against the table's checklist and notes.</summary>
    /// <param name="glance">What is in the room.</param>
    /// <param name="campaign">The table's checklist and notes.</param>
    /// <returns>The room's badges; <see cref="None"/> for a room with no glance.</returns>
    public static RoomBadges For(RoomGlance glance, CampaignState campaign)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        if (glance is null || glance.IsEmpty)
        {
            return None;
        }

        EncounterMonster[] stillUp = glance.Creatures
            .Where(creature => !campaign.IsCreatureCleared(glance.AreaNodeId, creature.NodeId))
            .ToArray();

        return new RoomBadges
        {
            StillUp = stillUp,
            Creatures = CreatureTally.Count(stillUp),
            Cleared = glance.Creatures.Count > 0 && stillUp.Length == 0,
            Loot = glance.Loot.Count > 0,
            Hazard = glance.Hazards.Count > 0,
            Secret = glance.HasSecret,
            Note = campaign.NoteFor(glance.AreaNodeId).Trim(),
        };
    }

    private string CreaturesTitle() =>
        string.Equals(Creatures, "1", StringComparison.Ordinal)
            ? "1 creature not yet ticked off"
            : Creatures + " creatures not yet ticked off";
}
