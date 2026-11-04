using DungeonTable.Web.Services;

namespace DungeonTable.Web.Rendering;

/// <summary>
/// The party token put somewhere on the map canvas: a square entered during a drag, the end of the drag,
/// or a spot clicked with the Party tool.
/// </summary>
/// <param name="WorldX">Where the token is, X in world units.</param>
/// <param name="WorldY">Where the token is, Y in world units.</param>
/// <param name="Kind">How it got there, which decides what the party saw on the way.</param>
public readonly record struct PartyStep(double WorldX, double WorldY, PartyMoveKind Kind);
