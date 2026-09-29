using DungeonTable.Core.Session;
using Qowaiv.Validation.Abstractions;

namespace DungeonTable.Application.Abstractions;

/// <summary>
/// Persists and restores the one live table — the reveal state and the running fight — so an app
/// restart does not cost an evening's play. Implemented by the Marten document-store adapter, and by
/// an ephemeral adapter used when no database is configured.
/// </summary>
/// <remarks>
/// There is no delete: ending a battle or clearing the reveals saves an empty snapshot, which is the
/// same write path as any other change and leaves one document per session rather than a row that
/// comes and goes.
/// </remarks>
public interface ISessionStore
{
    /// <summary>
    /// True when this store actually persists. False for the ephemeral adapter, so the DM screen can
    /// say so out loud rather than letting a DM assume the evening is being saved when it is not.
    /// </summary>
    bool IsPersistent { get; }

    /// <summary>Loads the persisted snapshot for a session.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The stored snapshot, or an invalid result when none exists or it could not be read.</returns>
    Task<Result<TableSnapshot>> LoadAsync(string sessionId, CancellationToken cancellationToken);

    /// <summary>Saves the snapshot for a session, replacing what was stored for it.</summary>
    /// <param name="snapshot">The snapshot to persist; its <c>SessionId</c> is the document key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see cref="Result.OK"/> on success, or the messages that caused failure.</returns>
    Task<Result> SaveAsync(TableSnapshot snapshot, CancellationToken cancellationToken);
}
