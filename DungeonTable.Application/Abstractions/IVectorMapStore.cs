using DungeonTable.Core.Maps.Vector;
using Qowaiv.Validation.Abstractions;

namespace DungeonTable.Application.Abstractions;

/// <summary>
/// Lists and loads the fully-vector maps (Dungeon Scrawl <c>.ds</c> sources) available under
/// the maps root. Implemented by the file-system adapter.
/// </summary>
public interface IVectorMapStore
{
    /// <summary>Lists the ids of all available vector maps.</summary>
    /// <returns>The known vector-map ids.</returns>
    IReadOnlyList<string> ListMapIds();

    /// <summary>Loads a single vector map by id.</summary>
    /// <param name="mapId">The map identifier (the <c>.ds</c> file name stem).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The parsed map, or an invalid result when the map is unknown or malformed.</returns>
    Task<Result<VectorMap>> LoadAsync(string mapId, CancellationToken cancellationToken);
}
