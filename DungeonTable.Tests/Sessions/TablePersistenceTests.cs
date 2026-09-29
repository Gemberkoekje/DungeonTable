using System.Threading;
using System.Threading.Tasks;
using DungeonTable.Core.Battle;
using DungeonTable.Core.Dossier;
using DungeonTable.Core.Session;
using DungeonTable.Core.Stats;
using DungeonTable.Tests.Battles;
using DungeonTable.Tests.Web;
using DungeonTable.Web.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace DungeonTable.Tests.Sessions;

/// <summary>
/// Verifies the persistence coordinator: it restores before anything can touch the table, writes only
/// when something has actually changed, retries a failed write instead of dropping it, and never
/// overwrites a document written by a newer build.
/// </summary>
/// <remarks>
/// The debounce is driven by calling <c>FlushAsync</c> directly rather than by waiting for timer ticks:
/// a test that sleeps for the save interval is slow and flaky, and the tick does nothing except call
/// this method.
/// </remarks>
public sealed class TablePersistenceTests
{
    private const string BugbearNode = "data_statblocks_monsters_bugbear";

    private static readonly DateTimeOffset StoredAt = new DateTimeOffset(2026, 7, 30, 20, 31, 0, TimeSpan.Zero);

    [Fact]
    public async Task Nothing_is_written_until_something_changes()
    {
        (TablePersistence persistence, _, _, _, FakeSessionStore store) = Build();
        await persistence.StartingAsync(CancellationToken.None);

        bool wrote = await persistence.FlushAsync(CancellationToken.None);

        Assert.False(wrote);
        Assert.Equal(0, store.SaveCount);
        Assert.False(persistence.HasUnsavedChanges);
    }

    [Fact]
    public async Task A_reveal_is_written_on_the_next_flush()
    {
        (TablePersistence persistence, SessionState session, _, _, FakeSessionStore store) = Build();
        await persistence.StartingAsync(CancellationToken.None);

        session.SetCurrentMap("level-1-undercroft");
        session.ToggleRegion("region-entrywell");
        Assert.True(persistence.HasUnsavedChanges);

        Assert.True(await persistence.FlushAsync(CancellationToken.None));

        Assert.Equal(1, store.SaveCount);
        Assert.Equal("table", store.Saved.SessionId);
        Assert.Equal(TableSnapshot.CurrentVersion, store.Saved.Version);
        Assert.Equal("region-entrywell", Assert.Single(store.Saved.Reveals.RevealedRegionIds));
        Assert.Equal(TableSaveKind.Saved, persistence.Status().Kind);
    }

    [Fact]
    public async Task A_whole_brush_stroke_is_written_once_rather_than_per_cell()
    {
        // The point of the debounce: painting fog raises Changed per cell, and each write is the whole
        // document.
        (TablePersistence persistence, SessionState session, _, _, FakeSessionStore store) = Build();
        await persistence.StartingAsync(CancellationToken.None);

        for (int col = 0; col < 40; col++)
        {
            session.PaintCell(col, 3, erase: false);
        }

        await persistence.FlushAsync(CancellationToken.None);

        Assert.Equal(1, store.SaveCount);
        Assert.Equal(40, store.Saved.Reveals.FogCells.Count);
    }

    [Fact]
    public async Task A_change_to_the_fight_is_written_too()
    {
        (TablePersistence persistence, _, BattleState battle, _, FakeSessionStore store) = Build();
        await persistence.StartingAsync(CancellationToken.None);

        battle.AddGroup(BugbearNode, "Bugbear", 2);
        await persistence.FlushAsync(CancellationToken.None);

        Assert.Equal(2, store.Saved.Battle.Combatants.Count);
        Assert.True(store.Saved.Battle.HasBattle());
    }

    [Fact]
    public async Task A_ticked_quest_beat_is_written_too()
    {
        (TablePersistence persistence, _, _, CampaignState campaign, FakeSessionStore store) = Build();
        await persistence.StartingAsync(CancellationToken.None);

        campaign.ToggleBeat("the-millers-son", "find-tam");
        campaign.Draw(new CardDeck
        {
            Id = "village-rumours",
            DrawRule = DeckDrawRule.Kept,
            Cards = new[] { new DeckCard { Id = "the-millers-debt" } },
        });
        await persistence.FlushAsync(CancellationToken.None);

        QuestProgress saved = Assert.Single(store.Saved.Campaign.Quests);
        Assert.Equal("the-millers-son", saved.QuestId);
        Assert.Equal("find-tam", Assert.Single(saved.CompletedBeatIds));
        DeckProgress deck = Assert.Single(store.Saved.Campaign.Decks);
        Assert.Equal("village-rumours", deck.DeckId);
        Assert.Equal("the-millers-debt", Assert.Single(deck.DrawnCardIds));
    }

    [Fact]
    public async Task A_stored_campaign_is_put_back_and_an_absent_one_starts_clean()
    {
        (TablePersistence persistence, _, _, CampaignState campaign, FakeSessionStore store) = Build();
        TableSnapshot stored = StoredTable();
        stored.Campaign = new CampaignSnapshot
        {
            Quests = new[]
            {
                new QuestProgress
                {
                    QuestId = "the-lost-hymnal",
                    Status = QuestStatus.Complete,
                    CompletedBeatIds = new[] { "tell-volo" },
                },
            },
            Decks = new[] { new DeckProgress { DeckId = "village-rumours", DrawnCardIds = new[] { "the-sextons-key" } } },
        };
        store.Stored = stored;

        Assert.True(await persistence.RestoreAsync(CancellationToken.None));
        Assert.Equal(QuestStatus.Complete, campaign.Status("the-lost-hymnal"));
        Assert.True(campaign.IsCardDrawn("village-rumours", "the-sextons-key"));

        // A document written before the quest log carries no campaign at all; the log must come up empty
        // rather than keeping whatever this process happened to hold.
        store.Stored = StoredTable();
        Assert.True(await persistence.RestoreAsync(CancellationToken.None));
        Assert.Equal(QuestStatus.None, campaign.Status("the-lost-hymnal"));
        Assert.Empty(campaign.DrawnCardIds("village-rumours"));
    }

    [Fact]
    public async Task A_second_flush_with_nothing_new_does_not_write_again()
    {
        (TablePersistence persistence, SessionState session, _, _, FakeSessionStore store) = Build();
        await persistence.StartingAsync(CancellationToken.None);

        session.ToggleRegion("region-entrywell");
        await persistence.FlushAsync(CancellationToken.None);
        await persistence.FlushAsync(CancellationToken.None);

        Assert.Equal(1, store.SaveCount);
    }

    [Fact]
    public async Task Restoring_does_not_by_itself_mark_the_table_dirty()
    {
        // Restore raises Changed on both services. Subscribing before the restore would write back
        // exactly what was just read, on every single start-up.
        (TablePersistence persistence, _, _, _, FakeSessionStore store) = Build();
        store.Stored = StoredTable();

        await persistence.StartingAsync(CancellationToken.None);

        Assert.False(persistence.HasUnsavedChanges);
        Assert.Equal(0, store.SaveCount);
    }

    [Fact]
    public async Task A_stored_table_is_put_back_into_both_services()
    {
        (TablePersistence persistence, SessionState session, BattleState battle, _, FakeSessionStore store) = Build();
        store.Stored = StoredTable();

        Assert.True(await persistence.RestoreAsync(CancellationToken.None));

        Assert.Equal("level-1-undercroft", session.CurrentMapId);
        Assert.Equal("region-entrywell", Assert.Single(session.RevealedRegionIds()));
        Assert.True(battle.IsActive);
        Assert.Equal("Nave", battle.Snapshot().AreaTitle);
        Assert.Equal(TableSaveKind.Saved, persistence.Status().Kind);
        Assert.Equal(StoredAt, persistence.Status().At);
    }

    [Fact]
    public async Task An_empty_database_leaves_a_fresh_table_and_says_so()
    {
        (TablePersistence persistence, SessionState session, BattleState battle, _, _) = Build();

        Assert.False(await persistence.RestoreAsync(CancellationToken.None));

        Assert.Equal(string.Empty, session.CurrentMapId);
        Assert.False(battle.IsActive);
        Assert.Equal(TableSaveKind.Waiting, persistence.Status().Kind);
    }

    [Fact]
    public async Task A_failed_write_is_kept_dirty_and_retried()
    {
        (TablePersistence persistence, SessionState session, _, _, FakeSessionStore store) = Build();
        await persistence.StartingAsync(CancellationToken.None);
        store.FailSaves = true;

        session.ToggleRegion("region-entrywell");
        Assert.False(await persistence.FlushAsync(CancellationToken.None));

        Assert.Equal(TableSaveKind.Failed, persistence.Status().Kind);
        Assert.Contains("The database is down.", persistence.Status().Message, StringComparison.Ordinal);
        Assert.True(persistence.HasUnsavedChanges);

        store.FailSaves = false;
        Assert.True(await persistence.FlushAsync(CancellationToken.None));

        Assert.Equal(TableSaveKind.Saved, persistence.Status().Kind);
        Assert.Equal("region-entrywell", Assert.Single(store.Saved.Reveals.RevealedRegionIds));
    }

    [Fact]
    public async Task A_failed_write_keeps_pointing_at_the_last_one_that_worked()
    {
        (TablePersistence persistence, SessionState session, _, _, FakeSessionStore store) = Build();
        await persistence.StartingAsync(CancellationToken.None);

        session.ToggleRegion("region-entrywell");
        await persistence.FlushAsync(CancellationToken.None);
        DateTimeOffset saved = persistence.Status().At;

        store.FailSaves = true;
        session.ToggleRegion("region-pillars");
        await persistence.FlushAsync(CancellationToken.None);

        Assert.Equal(saved, persistence.Status().At);
    }

    [Fact]
    public async Task A_document_from_a_newer_version_is_neither_restored_nor_overwritten()
    {
        (TablePersistence persistence, SessionState session, _, _, FakeSessionStore store) = Build();
        TableSnapshot newer = StoredTable();
        newer.Version = TableSnapshot.CurrentVersion + 1;
        store.Stored = newer;

        await persistence.StartingAsync(CancellationToken.None);
        session.ToggleRegion("region-pillars");
        bool wrote = await persistence.FlushAsync(CancellationToken.None);

        Assert.Equal(TableSaveKind.Blocked, persistence.Status().Kind);
        Assert.Equal(string.Empty, session.CurrentMapId);
        Assert.False(wrote);
        Assert.Equal(0, store.SaveCount);
    }

    [Fact]
    public async Task An_older_document_is_accepted()
    {
        (TablePersistence persistence, SessionState session, _, _, FakeSessionStore store) = Build();
        TableSnapshot older = StoredTable();
        older.Version = 0;
        store.Stored = older;

        Assert.True(await persistence.RestoreAsync(CancellationToken.None));

        Assert.Equal("level-1-undercroft", session.CurrentMapId);
    }

    [Fact]
    public async Task Without_a_database_nothing_is_read_or_written_and_the_status_says_not_saved()
    {
        var store = new FakeSessionStore { IsPersistent = false };
        var session = new SessionState();
        var offline = new TablePersistence(
            store,
            session,
            new BattleState(new FakePartyRoster(), new FakeStatLibrary()),
            new CampaignState(new FakePartyRoster()),
            "table",
            NullLogger<TablePersistence>.Instance);

        await offline.StartingAsync(CancellationToken.None);
        session.ToggleRegion("region-entrywell");
        bool wrote = await offline.FlushAsync(CancellationToken.None);

        Assert.Equal(TableSaveKind.Disabled, offline.Status().Kind);
        Assert.False(wrote);
        Assert.Equal(0, store.SaveCount);
        Assert.Equal(0, store.LoadCount);
        Assert.False(await offline.RestoreAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Shutting_down_takes_a_final_save()
    {
        (TablePersistence persistence, SessionState session, _, _, FakeSessionStore store) = Build();
        await persistence.StartingAsync(CancellationToken.None);

        session.ToggleRegion("region-entrywell");
        await persistence.StoppingAsync(CancellationToken.None);

        Assert.Equal(1, store.SaveCount);
        Assert.Equal("region-entrywell", Assert.Single(store.Saved.Reveals.RevealedRegionIds));
    }

    [Fact]
    public async Task Changes_after_shutdown_are_not_written()
    {
        (TablePersistence persistence, SessionState session, _, _, FakeSessionStore store) = Build();
        await persistence.StartingAsync(CancellationToken.None);
        await persistence.StoppingAsync(CancellationToken.None);

        session.ToggleRegion("region-entrywell");
        bool wrote = await persistence.FlushAsync(CancellationToken.None);

        Assert.False(wrote);
        Assert.Equal(0, store.SaveCount);
    }

    [Fact]
    public async Task Ending_a_battle_is_written_as_an_empty_fight_rather_than_left_stale()
    {
        (TablePersistence persistence, _, BattleState battle, _, FakeSessionStore store) = Build();
        await persistence.StartingAsync(CancellationToken.None);

        battle.AddGroup(BugbearNode, "Bugbear", 2);
        await persistence.FlushAsync(CancellationToken.None);
        battle.EndBattle();
        await persistence.FlushAsync(CancellationToken.None);

        Assert.Equal(2, store.SaveCount);
        Assert.False(store.Saved.Battle.HasBattle());
        Assert.Empty(store.Saved.Battle.Combatants);
    }

    [Fact]
    public async Task A_status_change_notifies_the_dm_screen()
    {
        (TablePersistence persistence, SessionState session, _, _, _) = Build();
        await persistence.StartingAsync(CancellationToken.None);
        int changes = 0;
        persistence.Changed += () => changes++;

        session.ToggleRegion("region-entrywell");
        await persistence.FlushAsync(CancellationToken.None);

        Assert.Equal(1, changes);
    }

    private static TableSnapshot StoredTable() => new TableSnapshot
    {
        SessionId = "table",
        Version = TableSnapshot.CurrentVersion,
        SavedAt = StoredAt,
        Reveals = new RevealSnapshot
        {
            CurrentMapId = "level-1-undercroft",
            SeededMapId = "level-1-undercroft",
            RevealedRegionIds = new[] { "region-entrywell" },
        },
        Battle = new BattleSnapshot
        {
            Id = "b1",
            AreaNodeId = "data_dossiers_level_1_area_3a",
            AreaTitle = "Nave",
            Round = 2,
            LastIdNumber = 4,
            NextSequence = 2,
            Entries = new[]
            {
                new InitiativeEntry
                {
                    Id = "e2",
                    Label = "Bugbears",
                    MemberIds = new[] { "c3" },
                    Kind = CombatantKind.Monster,
                    StatBlockNodeId = BugbearNode,
                },
            },
            Combatants = new[]
            {
                new Combatant
                {
                    Id = "c3",
                    Kind = CombatantKind.Monster,
                    StatBlockNodeId = BugbearNode,
                    Ordinal = 1,
                    Name = "Bugbear 1",
                    CurrentHp = 27,
                    MaxHp = 27,
                },
            },
        },
    };

    private static (TablePersistence Persistence, SessionState Session, BattleState Battle, CampaignState Campaign, FakeSessionStore Store) Build()
    {
        var store = new FakeSessionStore();
        var session = new SessionState();
        var stats = new FakeStatLibrary();
        stats.Monsters[BugbearNode] = new StatBlock
        {
            NodeId = BugbearNode,
            Name = "Bugbear",
            ArmourClass = 16,
            AverageHitPoints = 27,
            PassivePerception = 10,
            Abilities = new AbilityScores { Str = 15, Dex = 14, Con = 13, Intelligence = 8, Wis = 11, Cha = 9 },
        };

        var battle = new BattleState(new FakePartyRoster(), stats);
        var campaign = new CampaignState(new FakePartyRoster());
        var persistence = new TablePersistence(
            store,
            session,
            battle,
            campaign,
            "table",
            NullLogger<TablePersistence>.Instance);

        return (persistence, session, battle, campaign, store);
    }
}
