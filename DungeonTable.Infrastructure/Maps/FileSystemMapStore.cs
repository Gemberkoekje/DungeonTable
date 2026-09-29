using System.Threading;
using System.Threading.Tasks;
using DungeonTable.Application.Abstractions;
using DungeonTable.Core.Maps;
using DungeonTable.Infrastructure.Dossiers;
using Qowaiv.Validation.Abstractions;

namespace DungeonTable.Infrastructure.Maps;

/// <summary>
/// File-system adapter for <see cref="IMapStore"/>. Each map is a single
/// <c>{mapId}.regions.json</c> document under the maps root, grouped in per-adventure
/// subfolders. The document is a serialized <see cref="MapDefinition"/>; the filename stem
/// (minus the <c>.regions.json</c> suffix) is the map id. Immutable configuration, so this
/// is safe to use as a singleton.
/// </summary>
public sealed class FileSystemMapStore : IMapStore
{
    // Internal, like the options below, so the content tests find and read each map's document the
    // way this store does.
    internal const string DefinitionSuffix = ".regions.json";

    internal static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,

        // The Room Editor writes this document, but it can be edited by hand too: a null reads as if
        // the field were left out, and a null region or corner is dropped, as in every other document.
        // None of it changes a byte the editor writes.
        TypeInfoResolver = NullMeansLeftOut.Resolver(),
        Converters = { new NullTolerantValueConverter(), new LenientListConverter(), new LenientStringConverter() },
    };

    private readonly string mapsRoot;

    /// <summary>Creates a store over a maps root directory.</summary>
    /// <param name="mapsRoot">Absolute path to the directory holding the map assets.</param>
    public FileSystemMapStore(string mapsRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mapsRoot);
        this.mapsRoot = mapsRoot;
    }

    /// <inheritdoc />
    public IReadOnlyList<string> ListMapIds()
    {
        if (!Directory.Exists(mapsRoot))
        {
            return Array.Empty<string>();
        }

        return EnumerateDefinitionFiles()
            .Select(MapIdFromPath)
            .Where(id => !string.IsNullOrEmpty(id))
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();
    }

    /// <inheritdoc />
    public bool Exists(string mapId) =>
        !string.IsNullOrWhiteSpace(mapId) && !string.IsNullOrEmpty(FindDefinitionFile(mapId));

    /// <inheritdoc />
    public async Task<Result<MapDefinition>> LoadAsync(string mapId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(mapId))
        {
            return Result.WithMessages<MapDefinition>(
                ValidationMessage.Error("A map id is required.", nameof(mapId)));
        }

        string path = FindDefinitionFile(mapId);
        if (string.IsNullOrEmpty(path))
        {
            return Result.WithMessages<MapDefinition>(
                ValidationMessage.Error($"Map '{mapId}' was not found.", nameof(mapId)));
        }

        try
        {
            string json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            MapDefinition definition = JsonSerializer.Deserialize<MapDefinition>(json, Options);
            if (definition is null)
            {
                return Result.WithMessages<MapDefinition>(
                    ValidationMessage.Error($"Map '{mapId}' could not be read.", nameof(mapId)));
            }

            return Result.For(definition);
        }
        catch (JsonException exception)
        {
            return Result.WithMessages<MapDefinition>(
                ValidationMessage.Error($"Map '{mapId}' is not valid JSON: {exception.Message}", nameof(mapId)));
        }
        catch (IOException exception)
        {
            return Result.WithMessages<MapDefinition>(
                ValidationMessage.Error($"Map '{mapId}' could not be read: {exception.Message}", nameof(mapId)));
        }
        catch (UnauthorizedAccessException exception)
        {
            return Result.WithMessages<MapDefinition>(
                ValidationMessage.Error($"Map '{mapId}' could not be read: {exception.Message}", nameof(mapId)));
        }
    }

    /// <inheritdoc />
    public async Task<Result> SaveAsync(MapDefinition map, CancellationToken cancellationToken)
    {
        if (map is null || string.IsNullOrWhiteSpace(map.MapId))
        {
            return Result.WithMessages(
                ValidationMessage.Error("A map with a non-empty id is required.", nameof(map)));
        }

        string folder = string.IsNullOrWhiteSpace(map.Adventure)
            ? mapsRoot
            : Path.Combine(mapsRoot, map.Adventure);
        string path = Path.Combine(folder, map.MapId + DefinitionSuffix);

        // Written beside the target and moved over it, like the rosters: a crash or a full disk
        // mid-write leaves the last save whole, where writing in place could leave a truncated file,
        // and with it a map whose rooms, doors and secrets are all gone at once.
        string temp = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            Directory.CreateDirectory(folder);
            string json = JsonSerializer.Serialize(map, Options);
            await File.WriteAllTextAsync(temp, json, cancellationToken).ConfigureAwait(false);
            File.Move(temp, path, overwrite: true);
            return Result.OK;
        }
        catch (IOException exception)
        {
            return SaveFailed(map, temp, exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            return SaveFailed(map, temp, exception.Message);
        }
        catch (OperationCanceledException)
        {
            DeleteQuietly(temp);
            throw;
        }
    }

    // Drops the half-written temporary file (a leftover is harmless, but litters the maps folder) and
    // says why the save failed.
    private static Result SaveFailed(MapDefinition map, string temp, string reason)
    {
        DeleteQuietly(temp);
        return Result.WithMessages(
            ValidationMessage.Error($"Map '{map.MapId}' could not be saved: {reason}", nameof(map)));
    }

    private static void DeleteQuietly(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // Locked or already gone; nothing more to do.
        }
        catch (UnauthorizedAccessException)
        {
            // Same.
        }
    }

    private string FindDefinitionFile(string mapId)
    {
        if (!Directory.Exists(mapsRoot))
        {
            return string.Empty;
        }

        return EnumerateDefinitionFiles()
            .FirstOrDefault(path => string.Equals(MapIdFromPath(path), mapId, StringComparison.Ordinal))
            ?? string.Empty;
    }

    private IEnumerable<string> EnumerateDefinitionFiles() =>
        Directory
            .EnumerateFiles(mapsRoot, "*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(DefinitionSuffix, StringComparison.OrdinalIgnoreCase));

    private static string MapIdFromPath(string path)
    {
        string name = Path.GetFileName(path);
        return name.Length > DefinitionSuffix.Length && name.EndsWith(DefinitionSuffix, StringComparison.OrdinalIgnoreCase)
            ? name[..^DefinitionSuffix.Length]
            : string.Empty;
    }
}
