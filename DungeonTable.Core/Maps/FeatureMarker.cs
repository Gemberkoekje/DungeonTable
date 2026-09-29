using DungeonTable.Core.Briefing;

namespace DungeonTable.Core.Maps;

/// <summary>
/// A trap / hidden-door / secret marker placed at a point on a map. Individually
/// revealable: it stays hidden on the player view until its id is revealed, even
/// when the surrounding room is already visible.
/// </summary>
public sealed class FeatureMarker
{
    /// <summary>Stable identifier of the marker within its map.</summary>
    public string FeatureId { get; init; } = string.Empty;

    /// <summary>Marker position in map world units.</summary>
    public MapPoint Position { get; init; }

    /// <summary>The kind of feature this marker represents.</summary>
    public FeatureKind Kind { get; init; } = FeatureKind.None;

    /// <summary>Human-readable label.</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>Linked node id, or an empty string when unlinked.</summary>
    public string GraphNodeId { get; init; } = string.Empty;
}
