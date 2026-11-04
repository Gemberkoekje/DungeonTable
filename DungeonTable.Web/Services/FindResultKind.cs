namespace DungeonTable.Web.Services;

/// <summary>What a row of Find anything is, and so what opening it does.</summary>
public enum FindResultKind
{
    /// <summary>Unknown / unset.</summary>
    None = 0,

    /// <summary>Something found by its name: a room, a person, a monster, a spell, a book entry. It opens as a link would.</summary>
    Name = 1,

    /// <summary>A passage found by its words: it opens as a sheet over the dock.</summary>
    Text = 2,
}
