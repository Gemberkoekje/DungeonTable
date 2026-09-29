using DungeonTable.Application.Abstractions;
using DungeonTable.Core.Battle;
using Qowaiv.Validation.Abstractions;

namespace DungeonTable.Infrastructure.Rosters;

/// <summary>
/// File-system adapter for <see cref="IAllyRoster"/>, reading and writing <c>allies.json</c> in the
/// roster root. Identical in shape to <see cref="FileSystemPartyRoster"/> — see its remarks for why
/// neither caches.
/// </summary>
public sealed class FileSystemAllyRoster : IAllyRoster
{
    // Internal so the content tests read the same document.
    internal const string FileName = "allies.json";
    private const string What = "ally roster";

    private readonly string path;

    /// <summary>Creates a roster over a directory holding <c>allies.json</c>.</summary>
    /// <param name="rosterRoot">Absolute path to the directory holding the roster documents.</param>
    public FileSystemAllyRoster(string rosterRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rosterRoot);
        path = Path.Combine(rosterRoot, FileName);
    }

    /// <inheritdoc />
    public Result<AllyRoster> GetAllies() => RosterDocument.Read(path, new AllyRoster(), What);

    /// <inheritdoc />
    public Result Save(AllyRoster roster)
    {
        ArgumentNullException.ThrowIfNull(roster);
        return RosterDocument.Write(path, roster, What);
    }
}
