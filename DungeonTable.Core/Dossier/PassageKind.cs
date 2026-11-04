namespace DungeonTable.Core.Dossier;

/// <summary>
/// Where a <see cref="Passage"/> sits in the campaign, for the badge Find anything shows beside it and
/// the order its hits come in: the rules and the floor's facts first, since those are what the DM looks
/// up mid-session, then the rooms, then the rest.
/// </summary>
public enum PassageKind
{
    /// <summary>Unknown / unset.</summary>
    None = 0,

    /// <summary>A section of the campaign's rules reference.</summary>
    Rule = 1,

    /// <summary>One of a floor's standing facts: what dwells there, a faction, a wandering monster, the aftermath.</summary>
    Level = 2,

    /// <summary>A room's prose: its read-aloud, an at-a-glance bullet, a detail section.</summary>
    Area = 3,

    /// <summary>One of the campaign's named people: their summary, what they know, their hooks.</summary>
    Npc = 4,

    /// <summary>One of a floor's entities: an individual, a faction or an item, by its description.</summary>
    Entity = 5,

    /// <summary>A quest: its hook, a beat, a reward or a detail.</summary>
    Quest = 6,

    /// <summary>The table's session prep: a session's summary, canon, a scene or a decision.</summary>
    Session = 7,

    /// <summary>A card of one of the campaign's decks.</summary>
    Deck = 8,

    /// <summary>The campaign's background: a section, a bonus, the note on its level table.</summary>
    Campaign = 9,

    /// <summary>The party: the notes and open questions about it, or a player character's sheet.</summary>
    Party = 10,

    /// <summary>The table's earlier story: a chapter's summary, a section or a loose end.</summary>
    Story = 11,
}
