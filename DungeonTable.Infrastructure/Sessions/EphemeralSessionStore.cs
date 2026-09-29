using System.Threading;
using System.Threading.Tasks;
using DungeonTable.Application.Abstractions;
using DungeonTable.Core.Session;
using Qowaiv.Validation.Abstractions;

namespace DungeonTable.Infrastructure.Sessions;

/// <summary>
/// The <see cref="ISessionStore"/> used when no Postgres connection string is configured: the table
/// lives as long as the process and nothing is written anywhere.
/// </summary>
/// <remarks>
/// <para>
/// This exists so local development (and the test suite) needs no database, which is the same reason
/// the file-system stores exist for maps and dossiers. It is <b>not</b> a silent downgrade: it reports
/// <see cref="IsPersistent"/> as false, the composition root logs a warning naming the missing
/// connection string, and the DM screen's status bar says "not saved" for the whole session. A DM
/// finding out mid-restart that the evening was never being saved would be the worst possible way to
/// learn it.
/// </para>
/// <para>
/// It deliberately keeps nothing in memory either. The live <c>SessionState</c> and <c>BattleState</c>
/// singletons already survive a browser refresh on their own; a snapshot cached here would only be
/// read back by the same process that still holds the originals.
/// </para>
/// </remarks>
public sealed class EphemeralSessionStore : ISessionStore
{
    /// <inheritdoc />
    public bool IsPersistent => false;

    /// <inheritdoc />
    public Task<Result<TableSnapshot>> LoadAsync(string sessionId, CancellationToken cancellationToken) =>
        Task.FromResult(Result.WithMessages<TableSnapshot>(ValidationMessage.Error(
            "No database is configured, so there is no stored table to restore.", nameof(sessionId))));

    /// <inheritdoc />
    public Task<Result> SaveAsync(TableSnapshot snapshot, CancellationToken cancellationToken) =>
        Task.FromResult(Result.WithMessages(ValidationMessage.Error(
            "No database is configured; this table is not being saved.", nameof(snapshot))));
}
