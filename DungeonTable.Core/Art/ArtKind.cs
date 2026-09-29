namespace DungeonTable.Core.Art;

/// <summary>What a catalogued image depicts, for filtering the DM's picker.</summary>
public enum ArtKind
{
    /// <summary>Not stated.</summary>
    None = 0,

    /// <summary>A monster or other creature.</summary>
    Creature = 1,

    /// <summary>A named or nameable person the party can talk to.</summary>
    Npc = 2,

    /// <summary>A place: a hall, a castle, a vista.</summary>
    Location = 3,

    /// <summary>An object, treasure or magic item.</summary>
    Item = 4,

    /// <summary>Something written or drawn that the party is meant to look at as a prop.</summary>
    Handout = 5,

    /// <summary>A map, plan or schematic.</summary>
    Diagram = 6,
}
