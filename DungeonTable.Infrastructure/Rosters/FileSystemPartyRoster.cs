using DungeonTable.Application.Abstractions;
using DungeonTable.Core.Battle;
using Qowaiv.Validation.Abstractions;

namespace DungeonTable.Infrastructure.Rosters;

/// <summary>
/// File-system adapter for <see cref="IPartyRoster"/>, reading and writing <c>party.json</c> in the
/// roster root. Unlike the dossier and stat stores this one does <b>not</b> cache at construction:
/// the roster is edited in-app and must reflect the last save immediately, and it is a handful of
/// rows read a few times a session, so re-reading the file is cheaper than keeping a cache honest.
/// Safe as a singleton — the read is stateless and the write is atomic.
/// </summary>
public sealed class FileSystemPartyRoster : IPartyRoster
{
    // Internal so the content tests read the same document.
    internal const string FileName = "party.json";
    private const string What = "party roster";

    private readonly string path;

    /// <summary>Creates a roster over a directory holding <c>party.json</c>.</summary>
    /// <param name="rosterRoot">Absolute path to the directory holding the roster documents.</param>
    public FileSystemPartyRoster(string rosterRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rosterRoot);
        path = Path.Combine(rosterRoot, FileName);
    }

    /// <inheritdoc />
    public Result<Party> GetParty() => RosterDocument.Read(path, new Party(), What);

    /// <inheritdoc />
    public Result Save(Party party)
    {
        ArgumentNullException.ThrowIfNull(party);
        return RosterDocument.Write(path, party, What);
    }
}
