using DungeonTable.Core.Maps;
using Qowaiv.Validation.Abstractions;

namespace DungeonTable.Application.Abstractions;

/// <summary>
/// Loads and saves map definitions (<c>*.regions.json</c>). Implemented by the file-system
/// adapter.
/// </summary>
public interface IMapStore
{
    /// <summary>Lists the ids of all available maps.</summary>
    /// <returns>The known map ids.</returns>
    IReadOnlyList<string> ListMapIds();

    /// <summary>
    /// Indicates whether an annotation document already exists for a map. Used by the editor to
    /// tell a first-time map (safe to start empty) from one whose document failed to load (which
    /// must not be silently overwritten).
    /// </summary>
    /// <param name="mapId">The map identifier.</param>
    /// <returns><c>true</c> when a <c>*.regions.json</c> exists for the map.</returns>
    bool Exists(string mapId);

    /// <summary>Loads a single map definition.</summary>
    /// <param name="mapId">The map identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The map definition, or an invalid result when the map is unknown.</returns>
    Task<Result<MapDefinition>> LoadAsync(string mapId, CancellationToken cancellationToken);

    /// <summary>Persists a map definition authored in the region editor.</summary>
    /// <param name="map">The map definition to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result indicating success or the messages that caused failure.</returns>
    Task<Result> SaveAsync(MapDefinition map, CancellationToken cancellationToken);
}
