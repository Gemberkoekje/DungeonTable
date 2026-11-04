namespace DungeonTable.Web.Services;

/// <summary>How the party token got to where it was put, which decides what the party saw on the way.</summary>
public enum PartyMoveKind
{
    /// <summary>Not a move.</summary>
    None,

    /// <summary>
    /// Put down here (the Party tool): the party sees from here only, as if it had arrived by the stairs.
    /// A move of its own for Ctrl+Z.
    /// </summary>
    Place,

    /// <summary>
    /// The start of a drag: the party walks from where the token stood to here and sees from every square
    /// on the way. Starts a move of its own for Ctrl+Z.
    /// </summary>
    Walk,

    /// <summary>The same drag going on: the walk continues, and joins the move its start began.</summary>
    WalkOn,
}
