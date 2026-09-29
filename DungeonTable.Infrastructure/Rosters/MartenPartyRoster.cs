using System.Data.Common;
using System.Threading.Tasks;
using DungeonTable.Application.Abstractions;
using DungeonTable.Core.Battle;
using Marten;
using Marten.Exceptions;
using Qowaiv.Validation.Abstractions;

namespace DungeonTable.Infrastructure.Rosters;

/// <summary>
/// Marten document-store adapter for <see cref="IPartyRoster"/>: the party is one JSON document per
/// table, upserted whole. Safe as a singleton — it opens (and disposes) a session per call.
/// </summary>
/// <remarks>
/// <para>
/// This exists because the host's file system is <b>not</b> durable everywhere the app runs. On the
/// cluster the container writes into its own image layer, so a roster edited at the table was thrown
/// away by the next restart without anything saying so. The party is live table data — it belongs
/// next to the reveal state and the running fight, in Postgres.
/// </para>
/// <para>
/// <b>Seeding, and what it means.</b> The first read of a table that has no stored roster falls back
/// to the seed roster — the committed <c>data/party.json</c> — and writes what it finds back as the
/// document. From that moment the document is the only source of truth: editing
/// <c>data/party.json</c> in the repository and redeploying will <em>not</em> change the saved
/// roster, because the seed is never consulted again. That is deliberate. A redeploy must not
/// overwrite a party the DM edited at the table.
/// </para>
/// <para>
/// <b>Why the port stays synchronous.</b> Not convenience — two callers cannot await at all.
/// <c>BattleState.StartBattle</c> seeds the party from inside <c>lock (gate)</c>, and C# forbids
/// <c>await</c> in a lock body (CS1996); unpicking that means replacing every <c>lock</c> in a
/// singleton the DM circuit shares. <c>CampaignState.PartyLevel</c> is worse: it is read from inside
/// a LINQ predicate (<c>quest.Prerequisites.All(IsMet)</c>) and from a Razor render expression,
/// neither of which can await at all.
/// </para>
/// <para>
/// <b>Which is why this caches.</b> A synchronous port over a database is only honest if the calls
/// are not really going to the database. <c>PartyLevel</c> is evaluated per quest prerequisite on
/// every quest-list render, so without the cache this adapter would put a Postgres round-trip on a
/// render path and inside <c>BattleState</c>'s lock. The document is read once and then held; every
/// write goes through <see cref="Save"/>, which updates it. Safe because there is exactly one writer
/// — one app instance, one DM — which was equally true of the file this replaced. A second process
/// writing the same table would not be seen until this one restarts.
/// </para>
/// <para>
/// Every failure comes back as an invalid <c>Result</c> rather than an exception, matching
/// <see cref="Sessions.MartenSessionStore"/>: a database that has gone away degrades to "not saving,
/// and the DM can see it".
/// </para>
/// </remarks>
public sealed class MartenPartyRoster : IPartyRoster
{
    private readonly IDocumentStore store;
    private readonly IPartyRoster seed;
    private readonly string sessionId;

    // Guards `cached` only - never held across the database round-trip below, so two first calls
    // racing simply both load. That is harmless: they read the same document, and the seed write
    // they might both perform is an upsert.
    private readonly object gate = new object();

    // The roster as last read or written; null until the first successful read. See the remarks.
    private Party cached;

    /// <summary>Creates the adapter over a configured Marten store.</summary>
    /// <param name="store">The document store.</param>
    /// <param name="seed">
    /// Where a table with no stored roster gets its first one — the file-system roster over the
    /// committed <c>party.json</c>.
    /// </param>
    /// <param name="sessionId">The table identity the document is keyed by.</param>
    public MartenPartyRoster(IDocumentStore store, IPartyRoster seed, string sessionId)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(seed);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        this.store = store;
        this.seed = seed;
        this.sessionId = sessionId;
    }

    /// <inheritdoc />
    public Result<Party> GetParty()
    {
        lock (gate)
        {
            if (cached is not null)
            {
                return Result.For(cached);
            }
        }

        Result<Party> read = GetPartyAsync().GetAwaiter().GetResult();
        if (read.IsValid)
        {
            lock (gate)
            {
                cached = read.Value;
            }
        }

        // A failed read is deliberately not cached: the next call retries rather than serving an
        // error, or worse an empty party, for the rest of the evening.
        return read;
    }

    /// <inheritdoc />
    public Result Save(Party party)
    {
        ArgumentNullException.ThrowIfNull(party);

        Result written = SaveAsync(party).GetAwaiter().GetResult();
        if (written.IsValid)
        {
            lock (gate)
            {
                cached = party;
            }
        }

        return written;
    }

    private async Task<Result<Party>> GetPartyAsync()
    {
        try
        {
            await using IQuerySession session = store.QuerySession();
            StoredParty stored = await session.LoadAsync<StoredParty>(sessionId).ConfigureAwait(false);

            if (stored is not null)
            {
                return Result.For(stored.Party ?? new Party());
            }
        }
        catch (MartenCommandException error)
        {
            return Failed<Party>("read", error.Message);
        }
        catch (DbException error)
        {
            return Failed<Party>("read", error.Message);
        }
        catch (JsonException error)
        {
            return Failed<Party>("read", error.Message);
        }

        return await SeedAsync().ConfigureAwait(false);
    }

    private async Task<Result> SaveAsync(Party party)
    {
        try
        {
            await using IDocumentSession session = store.LightweightSession();
            session.Store(new StoredParty { SessionId = sessionId, Party = party });
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
    /// First read of a table that has never stored a roster: take the committed file's party and
    /// write it back as the document.
    /// </summary>
    /// <remarks>
    /// A failed seed <em>write</em> still returns the party it read. The roster is on screen either
    /// way, the next save retries the write, and blanking the DM's party because the database
    /// blinked would be the worse outcome. An empty seed is not written at all, so a fresh install
    /// does not create a document holding nothing.
    /// </remarks>
    /// <returns>The seeded party, or the seed's own error when the committed file is unreadable.</returns>
    private async Task<Result<Party>> SeedAsync()
    {
        Result<Party> fromFile = seed.GetParty();
        if (!fromFile.IsValid)
        {
            return fromFile;
        }

        Party party = fromFile.Value;
        if (party.Members.Count > 0)
        {
            await SaveAsync(party).ConfigureAwait(false);
        }

        return Result.For(party);
    }

    private static Result<T> Failed<T>(string what, string reason) =>
        Result.WithMessages<T>(ValidationMessage.Error(
            $"The party roster could not be {what}: {reason}", "postgres"));

    private static Result Failed(string what, string reason) =>
        Result.WithMessages(ValidationMessage.Error(
            $"The party roster could not be {what}: {reason}", "postgres"));
}
