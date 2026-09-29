namespace DungeonTable.ContentTests;

/// <summary>What the app reads a content file as. Public only so a theory can take one as a parameter.</summary>
public enum DocumentKind
{
    /// <summary>Not a document the app reads.</summary>
    None = 0,

    /// <summary>A <c>level-{n}.json</c>: a floor and its areas.</summary>
    Level,

    /// <summary>The rules reference, <c>reference.json</c>.</summary>
    Reference,

    /// <summary>The campaign background and level table, <c>campaign.json</c>.</summary>
    Campaign,

    /// <summary>The party dossier, <c>characters.json</c>.</summary>
    Characters,

    /// <summary>The NPC roster, <c>npcs.json</c>.</summary>
    Npcs,

    /// <summary>The story so far, <c>story.json</c>.</summary>
    Story,

    /// <summary>The session prep, <c>sessions.json</c>.</summary>
    Sessions,

    /// <summary>The campaign-wide relations, <c>relations.json</c>.</summary>
    Relations,

    /// <summary>A <c>quests*.json</c>.</summary>
    Quests,

    /// <summary>A <c>deck-*.json</c>.</summary>
    Deck,

    /// <summary>A <c>monsters*.json</c>.</summary>
    Monsters,

    /// <summary>A <c>spells*.json</c>.</summary>
    Spells,

    /// <summary>One book's index, in <c>data/book-index</c>.</summary>
    BookIndex,

    /// <summary>The committed art catalogue, <c>data/art/catalogue.json</c>.</summary>
    ArtCatalogue,

    /// <summary>A map's annotations, <c>{mapId}.regions.json</c>.</summary>
    MapRegions,

    /// <summary>A map's Dungeon Scrawl drawing, <c>{mapId}.ds</c>.</summary>
    MapDrawing,

    /// <summary>The party roster seed, <c>party.json</c>.</summary>
    PartyRoster,

    /// <summary>The ally roster seed, <c>allies.json</c>.</summary>
    AllyRoster,
}
