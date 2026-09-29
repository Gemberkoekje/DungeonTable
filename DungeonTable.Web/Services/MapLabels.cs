using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DungeonTable.Application.Abstractions;
using DungeonTable.Core.Maps;
using Qowaiv.Validation.Abstractions;

namespace DungeonTable.Web.Services;

/// <summary>
/// What a map picker calls each map: the level name its annotations give it, set in the Room Editor,
/// or the map's id while they give none. The DM screen's picker and the editor's share this, so the
/// name a DM types is the one they choose from at the table.
/// </summary>
public static class MapLabels
{
    /// <summary>Reads each map's level name from its annotations.</summary>
    /// <param name="annotations">The annotation store.</param>
    /// <param name="mapIds">The maps to name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The level name of every map that has one, by map id. A map with no annotations yet, or with
    /// ones that cannot be read, is left out, and is labelled by its id.
    /// </returns>
    public static async Task<IReadOnlyDictionary<string, string>> LoadAsync(
        IMapStore annotations, IEnumerable<string> mapIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(annotations);
        ArgumentNullException.ThrowIfNull(mapIds);

        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string mapId in mapIds)
        {
            Result<MapDefinition> result = await annotations.LoadAsync(mapId, cancellationToken);
            if (result.IsValid && !string.IsNullOrWhiteSpace(result.Value.LevelName))
            {
                names[mapId] = result.Value.LevelName.Trim();
            }
        }

        return names;
    }

    /// <summary>The label a map picker shows for a map.</summary>
    /// <param name="levelNames">The level names from <see cref="LoadAsync"/>.</param>
    /// <param name="mapId">The map.</param>
    /// <returns>The map's level name, or its id when it has none.</returns>
    public static string For(IReadOnlyDictionary<string, string> levelNames, string mapId) =>
        levelNames is not null && levelNames.TryGetValue(mapId ?? string.Empty, out string name) && !string.IsNullOrWhiteSpace(name)
            ? name
            : mapId ?? string.Empty;
}
