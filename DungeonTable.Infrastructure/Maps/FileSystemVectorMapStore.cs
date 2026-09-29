using System.Threading;
using System.Threading.Tasks;
using DungeonTable.Application.Abstractions;
using DungeonTable.Core.Maps.Vector;
using Qowaiv.Validation.Abstractions;

namespace DungeonTable.Infrastructure.Maps;

/// <summary>
/// File-system adapter for <see cref="IVectorMapStore"/>: each map is a Dungeon Scrawl
/// <c>{mapId}.ds</c> file under the maps root (grouped in per-adventure subfolders), parsed by
/// <see cref="DungeonScrawlMapReader"/>. Immutable configuration, so it is safe as a singleton.
/// </summary>
public sealed class FileSystemVectorMapStore : IVectorMapStore
{
    // Internal so the content tests find each map's drawing by the same name.
    internal const string SourceSuffix = ".ds";

    // Recurse but tolerate directories that vanish or are inaccessible mid-enumeration.
    private static readonly EnumerationOptions SourceEnumeration = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
    };

    private readonly string mapsRoot;
    private readonly IVectorMapReader reader;

    /// <summary>Creates a store over a maps root directory.</summary>
    /// <param name="mapsRoot">Absolute path to the directory holding the map assets.</param>
    /// <param name="reader">The Dungeon Scrawl reader used to parse each source.</param>
    public FileSystemVectorMapStore(string mapsRoot, IVectorMapReader reader)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mapsRoot);
        ArgumentNullException.ThrowIfNull(reader);
        this.mapsRoot = mapsRoot;
        this.reader = reader;
    }

    /// <inheritdoc />
    public IReadOnlyList<string> ListMapIds()
    {
        if (!Directory.Exists(mapsRoot))
        {
            return Array.Empty<string>();
        }

        try
        {
            return EnumerateSourceFiles()
                .Select(Path.GetFileNameWithoutExtension)
                .Where(id => !string.IsNullOrEmpty(id))
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();
        }
        catch (IOException)
        {
            return Array.Empty<string>();
        }
        catch (UnauthorizedAccessException)
        {
            return Array.Empty<string>();
        }
    }

    /// <inheritdoc />
    public Task<Result<VectorMap>> LoadAsync(string mapId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(mapId))
        {
            return Task.FromResult(Result.WithMessages<VectorMap>(
                ValidationMessage.Error("A map id is required.", nameof(mapId))));
        }

        string path = FindSourceFile(mapId);
        if (string.IsNullOrEmpty(path))
        {
            return Task.FromResult(Result.WithMessages<VectorMap>(
                ValidationMessage.Error($"Vector map '{mapId}' was not found.", nameof(mapId))));
        }

        return reader.ReadFileAsync(path, cancellationToken);
    }

    private string FindSourceFile(string mapId)
    {
        if (!Directory.Exists(mapsRoot))
        {
            return string.Empty;
        }

        try
        {
            return EnumerateSourceFiles()
                .FirstOrDefault(path =>
                    string.Equals(Path.GetFileNameWithoutExtension(path), mapId, StringComparison.Ordinal))
                ?? string.Empty;
        }
        catch (IOException)
        {
            return string.Empty;
        }
        catch (UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }

    private IEnumerable<string> EnumerateSourceFiles() =>
        Directory.EnumerateFiles(mapsRoot, "*" + SourceSuffix, SourceEnumeration);
}
