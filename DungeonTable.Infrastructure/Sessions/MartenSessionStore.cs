using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using DungeonTable.Application.Abstractions;
using DungeonTable.Core.Session;
using Marten;
using Marten.Exceptions;
using Qowaiv.Validation.Abstractions;

namespace DungeonTable.Infrastructure.Sessions;

/// <summary>
/// Marten document-store adapter for <see cref="ISessionStore"/>: the live table is one JSON document
/// per session, upserted whole. Safe as a singleton — it opens (and disposes) a session per call.
/// </summary>
/// <remarks>
/// Every failure comes back as an invalid <c>Result</c> rather than an exception. This is called from
/// a background loop while a fight is being run: a database that has gone away must degrade to "not
/// saving, and the DM can see it", never to a crashed host or a lost turn.
/// </remarks>
public sealed class MartenSessionStore : ISessionStore
{
    private readonly IDocumentStore store;

    /// <summary>Creates the adapter over a configured Marten store.</summary>
    /// <param name="store">The document store.</param>
    public MartenSessionStore(IDocumentStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        this.store = store;
    }

    /// <inheritdoc />
    public bool IsPersistent => true;

    /// <inheritdoc />
    public async Task<Result<TableSnapshot>> LoadAsync(string sessionId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return Result.WithMessages<TableSnapshot>(
                ValidationMessage.Error("A session id is required to load a table.", nameof(sessionId)));
        }

        try
        {
            await using IQuerySession session = store.QuerySession();
            TableSnapshot stored = await session
                .LoadAsync<TableSnapshot>(sessionId, cancellationToken)
                .ConfigureAwait(false);

            return stored is null
                ? Result.WithMessages<TableSnapshot>(ValidationMessage.Error(
                    $"No stored table for session '{sessionId}'.", nameof(sessionId)))
                : Result.For(stored);
        }
        catch (MartenCommandException error)
        {
            return Failed<TableSnapshot>("read", error.Message);
        }
        catch (DbException error)
        {
            return Failed<TableSnapshot>("read", error.Message);
        }
        catch (JsonException error)
        {
            // The document is there but no longer matches the shape this build reads. Report it; the
            // caller keeps the empty in-memory table rather than restoring half a fight.
            return Failed<TableSnapshot>("read", error.Message);
        }
    }

    /// <inheritdoc />
    public async Task<Result> SaveAsync(TableSnapshot snapshot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (string.IsNullOrWhiteSpace(snapshot.SessionId))
        {
            return Result.WithMessages(
                ValidationMessage.Error("A session id is required to save a table.", nameof(snapshot)));
        }

        try
        {
            await using IDocumentSession session = store.LightweightSession();
            session.Store(snapshot);
            await session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result.OK;
        }
        catch (MartenCommandException error)
        {
            return Failed("write", error.Message);
        }
        catch (DbException error)
        {
            return Failed("write", error.Message);
        }
        catch (JsonException error)
        {
            return Failed("write", error.Message);
        }
    }

    private static Result<T> Failed<T>(string what, string reason) =>
        Result.WithMessages<T>(ValidationMessage.Error($"The table could not be {what}: {reason}", "postgres"));

    private static Result Failed(string what, string reason) =>
        Result.WithMessages(ValidationMessage.Error($"The table could not be {what}: {reason}", "postgres"));
}
