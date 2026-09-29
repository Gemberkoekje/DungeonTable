namespace DungeonTable.Core.Dossier;

/// <summary>
/// Classifies a clickable cross-reference discovered in dossier prose, so the DM panel can pick
/// the right target view (jump to an area, or open a monster / spell / item / NPC reference card)
/// and colour the link by kind. Populated by the cross-reference resolver.
/// </summary>
public enum CrossRefKind
{
    /// <summary>Unknown / unset.</summary>
    None = 0,

    /// <summary>Another keyed area or dungeon level.</summary>
    Area = 1,

    /// <summary>A creature with a stat block.</summary>
    Monster = 2,

    /// <summary>A spell.</summary>
    Spell = 3,

    /// <summary>A magic item, from a rulebook or an encounter.</summary>
    Item = 4,

    /// <summary>A named NPC.</summary>
    Npc = 5,

    /// <summary>A faction or organisation.</summary>
    Faction = 6,

    /// <summary>An ability-check callout (non-navigating; shown as an informational pill).</summary>
    SkillCheck = 7,

    /// <summary>One of the party's player characters.</summary>
    Character = 8,

    /// <summary>
    /// An authored <c>[[…]]</c> link whose target names nothing that is loaded: misspelt, not
    /// authored yet, ambiguous, or an area with no floor to read it on. It shows its words as plain
    /// text, marked so the DM can see the link is broken, and never navigates.
    /// </summary>
    Unresolved = 9,

    /// <summary>
    /// An authored link to an area on a floor nobody has authored yet (<c>[[L2 area 14]]</c> before
    /// any <c>level-2.json</c> exists). It is not broken, only early: it shows its words, muted, and
    /// becomes a jump by itself once that floor is loaded.
    /// </summary>
    Pending = 10,

    /// <summary>
    /// An entry in the book index (<see cref="BookEntry"/>): something a book mentions that the
    /// campaign has no entry of its own for. Machine-made, so it is badged as the book's rather than
    /// styled like the campaign's own people and places.
    /// </summary>
    Book = 11,
}
