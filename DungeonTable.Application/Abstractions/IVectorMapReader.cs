using DungeonTable.Core.Maps.Vector;
using Qowaiv.Validation.Abstractions;

namespace DungeonTable.Application.Abstractions;

/// <summary>
/// Reads a fully-vector <see cref="VectorMap"/> from a Dungeon Scrawl <c>.ds</c> source.
/// Implemented by the Dungeon Scrawl adapter in the infrastructure layer.
/// </summary>
public interface IVectorMapReader
{
    /// <summary>Reads a <c>.ds</c> file from disk.</summary>
    /// <param name="filePath">Absolute path to the <c>.ds</c> file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The parsed map, or an invalid result when the file is missing or malformed.</returns>
    Task<Result<VectorMap>> ReadFileAsync(string filePath, CancellationToken cancellationToken);

    /// <summary>Reads a <c>.ds</c> archive from an in-memory byte array.</summary>
    /// <param name="dsContent">The raw <c>.ds</c> (ZIP) bytes.</param>
    /// <param name="mapId">Stable map id to assign.</param>
    /// <param name="adventure">Adventure to assign.</param>
    /// <returns>The parsed map, or an invalid result when the archive is malformed.</returns>
    Result<VectorMap> Read(byte[] dsContent, string mapId, string adventure);
}
