using System.Data.Common;
using System.Threading.Tasks;
using DungeonTable.Application.Abstractions;
using DungeonTable.Core.Battle;
using Marten;
using Marten.Exceptions;
using Qowaiv.Validation.Abstractions;

namespace DungeonTable.Infrastructure.Rosters;

/// <summary>
/// Marten document-store adapter for <see cref="IAllyRoster"/>. Mirrors
/// <see cref="MartenPartyRoster"/> exactly — see that type for the seed-once semantics, for why the
/// port is synchronous over an async-only Marten, and for why the document is cached once read.
/// </summary>
public sealed class MartenAllyRoster : IAllyRoster
{
    private readonly IDocumentStore store;
    private readonly IAllyRoster seed;
    private readonly string sessionId;

    // Guards `cached` only - see MartenPartyRoster for why racing first calls are harmless.
    private readonly object gate = new object();

    // The roster as last read or written; null until the first successful read.
    private AllyRoster cached;

    /// <summary>Creates the adapter over a configured Marten store.</summary>
    /// <param name="store">The document store.</param>
    /// <param name="seed">
    /// Where a table with no stored roster gets its first one — the file-system roster over the
    /// committed <c>allies.json</c>.
    /// </param>
    /// <param name="sessionId">The table identity the document is keyed by.</param>
    public MartenAllyRoster(IDocumentStore store, IAllyRoster seed, string sessionId)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(seed);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        this.store = store;
        this.seed = seed;
        this.sessionId = sessionId;
    }

    /// <inheritdoc />
    public Result<AllyRoster> GetAllies()
    {
        lock (gate)
        {
            if (cached is not null)
            {
                return Result.For(cached);
            }
        }

        Result<AllyRoster> read = GetAlliesAsync().GetAwaiter().GetResult();
        if (read.IsValid)
        {
            lock (gate)
            {
                cached = read.Value;
            }
        }

        // A failed read is deliberately not cached; the next call retries.
        return read;
    }

    /// <inheritdoc />
    public Result Save(AllyRoster roster)
    {
        ArgumentNullException.ThrowIfNull(roster);

        Result written = SaveAsync(roster).GetAwaiter().GetResult();
        if (written.IsValid)
        {
            lock (gate)
            {
                cached = roster;
            }
        }

        return written;
    }

    private async Task<Result<AllyRoster>> GetAlliesAsync()
    {
        try
        {
            await using IQuerySession session = store.QuerySession();
            StoredAllies stored = await session.LoadAsync<StoredAllies>(sessionId).ConfigureAwait(false);

            if (stored is not null)
            {
                return Result.For(stored.Roster ?? new AllyRoster());
            }
        }
        catch (MartenCommandException error)
        {
            return Failed<AllyRoster>("read", error.Message);
        }
        catch (DbException error)
        {
            return Failed<AllyRoster>("read", error.Message);
        }
        catch (JsonException error)
        {
            return Failed<AllyRoster>("read", error.Message);
        }

        return await SeedAsync().ConfigureAwait(false);
    }

    private async Task<Result> SaveAsync(AllyRoster roster)
    {
        try
        {
            await using IDocumentSession session = store.LightweightSession();
            session.Store(new StoredAllies { SessionId = sessionId, Roster = roster });
            await session.SaveChangesAsync().ConfigureAwait(false);
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

    /// <summary>
    /// First read of a table that has never stored allies: take the committed file's roster and
    /// write it back as the document. See <see cref="MartenPartyRoster"/> for why a failed seed
    /// write still returns what it read.
    /// </summary>
    /// <returns>The seeded roster, or the seed's own error when the committed file is unreadable.</returns>
    private async Task<Result<AllyRoster>> SeedAsync()
    {
        Result<AllyRoster> fromFile = seed.GetAllies();
        if (!fromFile.IsValid)
        {
            return fromFile;
        }

        AllyRoster roster = fromFile.Value;
        if (roster.Members.Count > 0)
        {
            await SaveAsync(roster).ConfigureAwait(false);
        }

        return Result.For(roster);
    }

    private static Result<T> Failed<T>(string what, string reason) =>
        Result.WithMessages<T>(ValidationMessage.Error(
            $"The ally roster could not be {what}: {reason}", "postgres"));

    private static Result Failed(string what, string reason) =>
        Result.WithMessages(ValidationMessage.Error(
            $"The ally roster could not be {what}: {reason}", "postgres"));
}
