namespace DungeonTable.Web.Services;

/// <summary>
/// How a door stood for the players at one moment: open or closed, and for a secret door whether they
/// had found it. Taken just before a click changes the door, so taking back what the opened door showed
/// can put the door back too (<see cref="SessionState.UndoPartyMove"/>).
/// </summary>
/// <param name="Id">The door's object id; empty for no door.</param>
/// <param name="Open">True when the door stood open.</param>
/// <param name="Discovered">True when the door was among the revealed features (a secret door found).</param>
public readonly record struct DoorState(string Id, bool Open, bool Discovered)
{
    /// <summary>No door: a reveal that came from no door at all.</summary>
    public static DoorState None { get; } = new DoorState(string.Empty, false, false);

    /// <summary>True when this names a door.</summary>
    public bool IsSet => !string.IsNullOrEmpty(Id);
}
