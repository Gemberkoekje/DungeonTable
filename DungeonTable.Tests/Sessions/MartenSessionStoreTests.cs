using System.Threading;
using System.Threading.Tasks;
using DungeonTable.Core.Battle;
using DungeonTable.Core.Session;
using DungeonTable.Infrastructure.Sessions;
using Marten;
using Qowaiv.Validation.Abstractions;

namespace DungeonTable.Tests.Sessions;

/// <summary>
/// Exercises the Marten adapter against a <b>real</b> PostgreSQL — the only way to prove the schema is
/// created, the string identity works, and a document written by one process is read back by the next.
/// </summary>
/// <remarks>
/// Skips without a test database — see <see cref="TestPostgres"/>. Each case uses its own session id,
/// so they neither collide with each other nor with a real table stored in the same database.
/// </remarks>
public sealed class MartenSessionStoreTests
{
    // Hoisted out of the assertions themselves (CA1861).
    private static readonly string[] ReplacedRegions = { "region-pillars", "region-crypt" };

    [Fact]
    public async Task A_table_written_by_one_store_is_read_back_by_the_next()
    {
        // Two stores over the same database stand in for two runs of the app: the second one has none
        // of the first one's in-memory state, which is the whole point of persisting the table.
        using DocumentStore first = OpenStore();
        string sessionId = SessionId(nameof(A_table_written_by_one_store_is_read_back_by_the_next));
        var writer = new MartenSessionStore(first);

        Result written = await writer.SaveAsync(Table(sessionId), CancellationToken.None);
        Assert.True(written.IsValid, Explain(written));

        using DocumentStore second = OpenStore();
        Result<TableSnapshot> read = await new MartenSessionStore(second)
            .LoadAsync(sessionId, CancellationToken.None);

        Assert.True(read.IsValid, Explain(read));
        TableSnapshot restored = read.Value;
        Assert.Equal(sessionId, restored.SessionId);
        Assert.Equal("region-entrywell", Assert.Single(restored.Reveals.RevealedRegionIds));
        Assert.Equal(4, Assert.Single(restored.Reveals.FogCells).Col);
        Assert.Equal("Nave", restored.Battle.AreaTitle);

        Combatant combatant = Assert.Single(restored.Battle.Combatants);
        Assert.Equal("Bugbear 1", combatant.Name);
        Assert.Equal(7, combatant.CurrentHp);
        Assert.Equal(ConditionKind.Prone, Assert.Single(combatant.Conditions));
        Assert.True(combatant.Bloodied);
    }

    [Fact]
    public async Task Saving_twice_replaces_the_document_rather_than_adding_one()
    {
        using DocumentStore store = OpenStore();
        string sessionId = SessionId(nameof(Saving_twice_replaces_the_document_rather_than_adding_one));
        var adapter = new MartenSessionStore(store);

        await adapter.SaveAsync(Table(sessionId), CancellationToken.None);
        TableSnapshot second = Table(sessionId);
        second.Reveals.RevealedRegionIds = ReplacedRegions;
        await adapter.SaveAsync(second, CancellationToken.None);

        Result<TableSnapshot> read = await adapter.LoadAsync(sessionId, CancellationToken.None);

        Assert.True(read.IsValid, Explain(read));
        Assert.Equal(ReplacedRegions, read.Value.Reveals.RevealedRegionIds);

        await using IQuerySession query = store.QuerySession();
        Assert.Equal(1, await query.Query<TableSnapshot>().CountAsync(
            snapshot => snapshot.SessionId == sessionId,
            CancellationToken.None));
    }

    [Fact]
    public async Task An_unknown_session_reports_that_nothing_is_stored()
    {
        using DocumentStore store = OpenStore();

        Result<TableSnapshot> read = await new MartenSessionStore(store)
            .LoadAsync(SessionId("never-written"), CancellationToken.None);

        Assert.False(read.IsValid);
    }

    [Fact]
    public async Task An_unreachable_database_fails_as_a_result_rather_than_an_exception()
    {
        // The failure that matters at the table: Postgres has gone away mid-fight. The app must carry on
        // and say "not saved", never crash the circuit.
        TestPostgres.RequireConnection();
        var options = new StoreOptions();
        SessionDocuments.Configure(
            options,
            "Host=localhost;Port=1;Database=nope;Username=nope;Password=nope;Timeout=2");
        options.AutoCreateSchemaObjects = JasperFx.AutoCreate.None;
        using IDocumentStore store = new DocumentStore(options);
        var adapter = new MartenSessionStore(store);

        Result saved = await adapter.SaveAsync(Table(SessionId("offline")), CancellationToken.None);
        Result<TableSnapshot> read = await adapter.LoadAsync(SessionId("offline"), CancellationToken.None);

        Assert.False(saved.IsValid);
        Assert.False(read.IsValid);
    }

    [Fact]
    public async Task A_session_id_is_required()
    {
        using DocumentStore store = OpenStore();
        var adapter = new MartenSessionStore(store);

        Result saved = await adapter.SaveAsync(Table("  "), CancellationToken.None);
        Result<TableSnapshot> read = await adapter.LoadAsync(string.Empty, CancellationToken.None);

        Assert.False(saved.IsValid);
        Assert.False(read.IsValid);
    }

    private static DocumentStore OpenStore()
    {
        var options = new StoreOptions();
        SessionDocuments.Configure(options, TestPostgres.RequireConnection());
        return new DocumentStore(options);
    }

    // Namespaced per case so the cases are independent of each other and of any real stored table.
    private static string SessionId(string name) => $"test-{name}";

    private static string Explain(Result result) =>
        string.Join(" ", result.Messages.Select(message => message.Message));

    private static string Explain<T>(Result<T> result) =>
        string.Join(" ", result.Messages.Select(message => message.Message));

    private static TableSnapshot Table(string sessionId) => new TableSnapshot
    {
        SessionId = sessionId,
        Version = TableSnapshot.CurrentVersion,
        SavedAt = DateTimeOffset.UtcNow,
        Reveals = new RevealSnapshot
        {
            CurrentMapId = "level-1-undercroft",
            SeededMapId = "level-1-undercroft",
            RevealedRegionIds = new[] { "region-entrywell" },
            RevealedFeatureIds = new[] { "trap-pit-1" },
            OpenDoorIds = new[] { "door-7" },
            FogCells = new[] { new FogCell { Col = 4, Row = 9 } },
            PlayerViewport = new ViewportSnapshot { MinX = 100, MinY = 200, MaxX = 1_100, MaxY = 762.5 },
            PlayerAspect = 16.0 / 9.0,
        },
        Battle = new BattleSnapshot
        {
            Id = "b1",
            AreaNodeId = "data_dossiers_level_1_area_3a",
            AreaTitle = "Nave",
            Round = 3,
            Started = true,
            LastIdNumber = 5,
            NextSequence = 2,
            IssuedOrdinals = new[] { new IssuedOrdinal { TypeKey = "data_statblocks_monsters_bugbear", Issued = 1 } },
            Entries = new[]
            {
                new InitiativeEntry
                {
                    Id = "e2",
                    Sequence = 1,
                    Initiative = 17,
                    HasInitiative = true,
                    Label = "Bugbears",
                    MemberIds = new[] { "c5" },
                    Kind = CombatantKind.Monster,
                    StatBlockNodeId = "data_statblocks_monsters_bugbear",
                },
            },
            Combatants = new[]
            {
                new Combatant
                {
                    Id = "c5",
                    Kind = CombatantKind.Monster,
                    StatBlockNodeId = "data_statblocks_monsters_bugbear",
                    Ordinal = 1,
                    Name = "Bugbear 1",
                    CurrentHp = 7,
                    MaxHp = 27,
                    ArmourClass = 16,
                    PassivePerception = 10,
                    Conditions = new[] { ConditionKind.Prone },
                    Notes = "fled east",
                },
            },
        },
    };
}
