using System.Collections.Generic;
using System.Linq;

namespace DungeonTable.Web.Rendering;

/// <summary>
/// Which imported objects (stairs and decoration sprites) are concealed from the players, keyed by
/// source object id. <see cref="ConcealedIds"/> is authored in the Room Editor and saved with the
/// map; <see cref="RevealedIds"/> is the live session state the DM toggles during play. An object
/// that is concealed and not yet revealed is drawn ghosted on the DM's own map and is omitted
/// entirely from the player projector — the same author-then-discover split secret doors use
/// (<see cref="DoorRenderState"/>), which is why both read the one shared reveal set.
/// </summary>
public sealed class ConcealState
{
    /// <summary>An empty state: nothing is concealed.</summary>
    public static ConcealState None { get; } = new ConcealState();

    /// <summary>Ids of objects authored concealed.</summary>
    public IReadOnlyCollection<string> ConcealedIds { get; init; } = Array.Empty<string>();

    /// <summary>Ids of objects the DM has revealed to the players for this session.</summary>
    public IReadOnlyCollection<string> RevealedIds { get; init; } = Array.Empty<string>();

    /// <summary>True when nothing is concealed, so the renderer can skip the lookups entirely.</summary>
    public bool IsEmpty => ConcealedIds.Count == 0;

    /// <summary>True when the object is concealed AND not yet revealed — i.e. the players must not see it.</summary>
    /// <param name="objectId">The source object id.</param>
    /// <returns>True when the object is still hidden from the players.</returns>
    public bool IsHidden(string objectId) =>
        ConcealedIds.Contains(objectId) && !RevealedIds.Contains(objectId);
}
