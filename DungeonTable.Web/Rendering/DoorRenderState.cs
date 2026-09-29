using System.Collections.Generic;
using System.Linq;

namespace DungeonTable.Web.Rendering;

/// <summary>
/// The per-door presentation state the renderer needs, keyed by door object id. The geometry comes
/// from the map; these sets say how each door is drawn: whether it is still secret (drawn as
/// anonymous wall), which secret doors have been discovered (then drawn as a normal door), which
/// doors are open, and which are double doors. All sets default to empty.
/// </summary>
public sealed class DoorRenderState
{
    /// <summary>An empty state: every door is a plain, closed, single, non-secret door.</summary>
    public static DoorRenderState None { get; } = new DoorRenderState();

    /// <summary>Ids of doors authored secret. A secret door renders as anonymous wall until it is
    /// discovered (see <see cref="DiscoveredDoorIds"/>).</summary>
    public IReadOnlyCollection<string> SecretDoorIds { get; init; } = Array.Empty<string>();

    /// <summary>Ids of secret doors the DM has discovered for the players; such a door renders as a
    /// normal door (open or closed) rather than wall.</summary>
    public IReadOnlyCollection<string> DiscoveredDoorIds { get; init; } = Array.Empty<string>();

    /// <summary>Ids of doors that are currently open.</summary>
    public IReadOnlyCollection<string> OpenDoorIds { get; init; } = Array.Empty<string>();

    /// <summary>Ids of doors drawn as double doors (a pair of leaves) rather than a single leaf.</summary>
    public IReadOnlyCollection<string> DoubleDoorIds { get; init; } = Array.Empty<string>();

    /// <summary>True when the door is secret AND not yet discovered — i.e. it must render as wall.</summary>
    /// <param name="doorId">The door object id.</param>
    /// <returns>True when the door should be drawn as anonymous wall.</returns>
    public bool IsHiddenSecret(string doorId) =>
        SecretDoorIds.Contains(doorId) && !DiscoveredDoorIds.Contains(doorId);
}
