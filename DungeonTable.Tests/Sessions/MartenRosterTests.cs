using System.Threading.Tasks;
using DungeonTable.Application.Abstractions;
using DungeonTable.Core.Battle;
using DungeonTable.Infrastructure.Rosters;
using DungeonTable.Infrastructure.Sessions;
using Marten;
using Qowaiv.Validation.Abstractions;

namespace DungeonTable.Tests.Sessions;

/// <summary>
/// Exercises the Marten roster adapters against a <b>real</b> PostgreSQL, which is the only way to
/// prove the documents survive a restart — the whole reason they moved off the file system.
/// </summary>
/// <remarks>
/// <para>
/// Skips without a test database — see <see cref="TestPostgres"/>. Each case uses its own session
/// id, so they neither collide with each other nor with a real stored table.
/// </para>
/// <para>
/// Every case <b>deletes its own documents first</b>. Most of these assert on what happens when a
/// table has never been stored, so without that they would pass once against a virgin database and
/// fail on every run after it — which is exactly what they did before this was added.
/// </para>
/// </remarks>
public sealed class MartenRosterTests
{
    // Hoisted out of the assertions themselves (CA1861).
    private static readonly string[] EditedParty = { "Sora", "Rurik" };
    private static readonly string[] EditedAllies = { "Bonnie", "Scrap" };

    [Fact]
    public async Task A_party_saved_by_one_store_is_read_back_by_the_next()
    {
        // Two stores over the same database stand in for two runs of the app - a pod restart.
        using DocumentStore first = OpenStore();
        string sessionId = SessionId(nameof(A_party_saved_by_one_store_is_read_back_by_the_next));
        await CleanAsync(first, sessionId);

        Result written = Party(first, sessionId, Seed("Finway")).Save(PartyOf("Sora", "Rurik"));
        Assert.True(written.IsValid, Explain(written));

        using DocumentStore second = OpenStore();
        Result<Party> read = Party(second, sessionId, Seed("Finway")).GetParty();

        Assert.True(read.IsValid, Explain(read));
        Assert.Equal(EditedParty, read.Value.Members.Select(member => member.Name));
    }

    [Fact]
    public async Task A_table_with_no_document_is_seeded_from_the_committed_file()
    {
        using DocumentStore store = OpenStore();
        string sessionId = SessionId(nameof(A_table_with_no_document_is_seeded_from_the_committed_file));
        await CleanAsync(store, sessionId);

        Result<Party> read = Party(store, sessionId, Seed("Finway")).GetParty();

        Assert.True(read.IsValid, Explain(read));
        Assert.Equal("Finway", Assert.Single(read.Value.Members).Name);
    }

    [Fact]
    public async Task Once_seeded_the_document_wins_and_the_committed_file_is_ignored()
    {
        // The behaviour the deployment depends on: a redeploy carrying a different data/party.json
        // must NOT overwrite the roster the DM edited at the table.
        using DocumentStore store = OpenStore();
        string sessionId = SessionId(nameof(Once_seeded_the_document_wins_and_the_committed_file_is_ignored));
        await CleanAsync(store, sessionId);

        Assert.True(Party(store, sessionId, Seed("Finway")).GetParty().IsValid);
        Result edited = Party(store, sessionId, Seed("Finway")).Save(PartyOf("Sora"));
        Assert.True(edited.IsValid, Explain(edited));

        // A later run whose committed file says something else entirely.
        Result<Party> read = Party(store, sessionId, Seed("SomebodyElse")).GetParty();

        Assert.True(read.IsValid, Explain(read));
        Assert.Equal("Sora", Assert.Single(read.Value.Members).Name);
    }

    [Fact]
    public async Task An_empty_seed_is_not_written_so_a_fresh_install_stays_unseeded()
    {
        using DocumentStore store = OpenStore();
        string sessionId = SessionId(nameof(An_empty_seed_is_not_written_so_a_fresh_install_stays_unseeded));
        await CleanAsync(store, sessionId);

        Result<Party> read = Party(store, sessionId, new FakeSeedParty(new Party())).GetParty();
        Assert.True(read.IsValid, Explain(read));
        Assert.Empty(read.Value.Members);

        // Nothing was stored, so a later run with a real committed file still seeds from it.
        Result<Party> later = Party(store, sessionId, Seed("Finway")).GetParty();
        Assert.Equal("Finway", Assert.Single(later.Value.Members).Name);
    }

    [Fact]
    public async Task A_corrupt_seed_is_reported_rather_than_silently_replaced_by_an_empty_party()
    {
        using DocumentStore store = OpenStore();
        string sessionId = SessionId(nameof(A_corrupt_seed_is_reported_rather_than_silently_replaced_by_an_empty_party));
        await CleanAsync(store, sessionId);

        Result<Party> read = Party(store, sessionId, new FakeSeedParty(broken: true)).GetParty();

        Assert.False(read.IsValid);
    }

    [Fact]
    public async Task Allies_round_trip_and_seed_the_same_way()
    {
        using DocumentStore store = OpenStore();
        string sessionId = SessionId(nameof(Allies_round_trip_and_seed_the_same_way));
        await CleanAsync(store, sessionId);
        var seed = new FakeSeedAllies(AlliesOf("Shadow"));

        Result<AllyRoster> seeded = new MartenAllyRoster(store, seed, sessionId).GetAllies();
        Assert.True(seeded.IsValid, Explain(seeded));
        Assert.Equal("Shadow", Assert.Single(seeded.Value.Members).Name);

        Result written = new MartenAllyRoster(store, seed, sessionId).Save(AlliesOf("Bonnie", "Scrap"));
        Assert.True(written.IsValid, Explain(written));

        using DocumentStore second = OpenStore();
        Result<AllyRoster> read = new MartenAllyRoster(second, seed, sessionId).GetAllies();
        Assert.Equal(EditedAllies, read.Value.Members.Select(member => member.Name));
    }

    [Fact]
    public async Task The_party_and_ally_documents_do_not_collide_on_the_shared_table_id()
    {
        using DocumentStore store = OpenStore();
        string sessionId = SessionId(nameof(The_party_and_ally_documents_do_not_collide_on_the_shared_table_id));
        await CleanAsync(store, sessionId);

        Assert.True(Party(store, sessionId, Seed("Finway")).Save(PartyOf("Sora")).IsValid);
        Assert.True(new MartenAllyRoster(store, new FakeSeedAllies(new AllyRoster()), sessionId)
            .Save(AlliesOf("Shadow")).IsValid);

        Assert.Equal("Sora", Assert.Single(Party(store, sessionId, Seed("Finway")).GetParty().Value.Members).Name);
        Assert.Equal("Shadow", Assert.Single(new MartenAllyRoster(
            store, new FakeSeedAllies(new AllyRoster()), sessionId).GetAllies().Value.Members).Name);
    }

    [Fact]
    public async Task One_adapter_serves_the_roster_from_memory_after_the_first_read()
    {
        // CampaignState.PartyLevel is evaluated per quest prerequisite on every quest-list render,
        // from inside a LINQ predicate that cannot await. It must not reach Postgres every time.
        // Proven by changing the document underneath a loaded adapter: a caching one keeps its own
        // answer, an adapter that re-queried would pick the new document up.
        using DocumentStore store = OpenStore();
        string sessionId = SessionId(nameof(One_adapter_serves_the_roster_from_memory_after_the_first_read));
        await CleanAsync(store, sessionId);

        MartenPartyRoster loaded = Party(store, sessionId, Seed("Finway"));
        Assert.Equal("Finway", Assert.Single(loaded.GetParty().Value.Members).Name);

        Assert.True(Party(store, sessionId, Seed("Finway")).Save(PartyOf("Sora")).IsValid);

        Assert.Equal("Finway", Assert.Single(loaded.GetParty().Value.Members).Name);
        Assert.Equal("Sora", Assert.Single(Party(store, sessionId, Seed("Finway")).GetParty().Value.Members).Name);
    }

    [Fact]
    public async Task A_save_updates_what_the_same_adapter_reads_back()
    {
        using DocumentStore store = OpenStore();
        string sessionId = SessionId(nameof(A_save_updates_what_the_same_adapter_reads_back));
        await CleanAsync(store, sessionId);

        MartenPartyRoster roster = Party(store, sessionId, Seed("Finway"));
        Assert.Equal("Finway", Assert.Single(roster.GetParty().Value.Members).Name);

        Assert.True(roster.Save(PartyOf("Sora")).IsValid);

        // The write-through half of the cache: an editor that saves must not then re-read its own
        // stale copy.
        Assert.Equal("Sora", Assert.Single(roster.GetParty().Value.Members).Name);
    }

    // ---- Helpers -----------------------------------------------------------------------------

    /// <summary>
    /// Puts the table back to "never stored", so a case asserting on the unseeded path is testing
    /// that path and not the leftovers of its own previous run.
    /// </summary>
    /// <param name="store">The store to clean (concrete: CA1859, it is only ever the test's own).</param>
    /// <param name="sessionId">The table identity to clear both roster documents for.</param>
    /// <returns>A task that completes once both documents are gone.</returns>
    private static async Task CleanAsync(DocumentStore store, string sessionId)
    {
        await using IDocumentSession session = store.LightweightSession();
        session.Delete<StoredParty>(sessionId);
        session.Delete<StoredAllies>(sessionId);
        await session.SaveChangesAsync();
    }

    private static MartenPartyRoster Party(IDocumentStore store, string sessionId, IPartyRoster seed) =>
        new MartenPartyRoster(store, seed, sessionId);

    private static FakeSeedParty Seed(string name) => new FakeSeedParty(PartyOf(name));

    private static Party PartyOf(params string[] names) => new Party
    {
        Members = names.Select(name => new PartyMember { Name = name, Level = 5 }).ToList(),
    };

    private static AllyRoster AlliesOf(params string[] names) => new AllyRoster
    {
        Members = names.Select(name => new AllyMember { Name = name }).ToList(),
    };

    private static DocumentStore OpenStore()
    {
        var options = new StoreOptions();
        SessionDocuments.Configure(options, TestPostgres.RequireConnection());
        return new DocumentStore(options);
    }

    // Namespaced per case so the cases are independent of each other and of any real stored table.
    private static string SessionId(string name) => $"test-roster-{name}";

    private static string Explain(Result result) =>
        string.Join(" ", result.Messages.Select(message => message.Message));

    private static string Explain<T>(Result<T> result) =>
        string.Join(" ", result.Messages.Select(message => message.Message));

    /// <summary>Stands in for the committed <c>party.json</c> without touching the file system.</summary>
    private sealed class FakeSeedParty : IPartyRoster
    {
        private readonly Party seeded;
        private readonly bool broken;

        internal FakeSeedParty(Party seeded)
        {
            this.seeded = seeded;
        }

        internal FakeSeedParty(bool broken)
        {
            seeded = new Party();
            this.broken = broken;
        }

        public Result<Party> GetParty() => broken
            ? Result.WithMessages<Party>(ValidationMessage.Error("party.json is corrupt.", "seed"))
            : Result.For(seeded);

        public Result Save(Party party) => Result.OK;
    }

    /// <summary>Stands in for the committed <c>allies.json</c>.</summary>
    private sealed class FakeSeedAllies : IAllyRoster
    {
        private readonly AllyRoster seeded;

        internal FakeSeedAllies(AllyRoster seeded)
        {
            this.seeded = seeded;
        }

        public Result<AllyRoster> GetAllies() => Result.For(seeded);

        public Result Save(AllyRoster roster) => Result.OK;
    }
}
