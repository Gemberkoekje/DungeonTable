using System.Threading;
using System.Threading.Tasks;
using DungeonTable.Core.Battle;
using DungeonTable.Core.Dossier;
using DungeonTable.Core.Session;
using DungeonTable.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DungeonTable.Tests.Sessions;

/// <summary>
/// The whole point of persistence, end to end: a table set up in one run of the app is still there in
/// the next one. Two <see cref="WebApplicationFactory{TEntryPoint}"/> hosts stand in for the restart, so this
/// goes through the <b>real composition root</b> — the Marten registration, the hosted service, the
/// document contract and PostgreSQL itself.
/// </summary>
/// <remarks>
/// Skips without a test database — see <see cref="TestPostgres"/>. The narrower unit tests
/// (<see cref="TablePersistenceTests"/>) cover the same behaviour against a fake store and always run.
/// </remarks>
public sealed class AppRestartTests
{
    private const string BugbearNode = "data_statblocks_monsters_bugbear";

    [Fact]
    public async Task A_fight_and_its_reveals_are_still_there_after_a_restart()
    {
        string connection = TestPostgres.RequireConnection();
        string sessionId = $"test-restart-{Guid.NewGuid():N}";

        string bugbearId;
        using (WebApplicationFactory<Program> before = Host(connection, sessionId))
        {
            var session = before.Services.GetRequiredService<SessionState>();
            var battle = before.Services.GetRequiredService<BattleState>();

            session.SetCurrentMap(SamplePack.MapId);
            session.MarkSeeded(SamplePack.MapId);
            session.ToggleRegion("region-landing");
            session.ToggleFeature("trap-pit-1");
            session.SetDoorOpen("door-7", open: true);
            session.PaintCell(10, 12, erase: false);

            battle.StartBattle("data_dossiers_level_1_area_4", "Bell-Founder's Workshop");
            battle.AddGroup(BugbearNode, "Bugbear", 3);
            Battle running = battle.Snapshot();
            bugbearId = running.Combatants.Single(combatant => combatant.Ordinal == 2).Id;
            battle.ApplyDamage(bugbearId, 20);
            battle.ToggleCondition(bugbearId, ConditionKind.Prone);
            battle.SetInitiative(running.Entries[0].Id, 17);
            battle.NextTurn();

            var campaign = before.Services.GetRequiredService<CampaignState>();
            campaign.ToggleBeat("the-millers-son", "find-tam");
            campaign.SetStatus("the-lost-hymnal", QuestStatus.Complete);
            campaign.Draw(new CardDeck
            {
                Id = "village-rumours",
                DrawRule = DeckDrawRule.Kept,
                Cards = new[] { new DeckCard { Id = "the-millers-debt" } },
            });

            // The save loop would take this within a couple of seconds; the test does not wait for a tick.
            var persistence = before.Services.GetRequiredService<TablePersistence>();
            Assert.True(await persistence.FlushAsync(CancellationToken.None));
            Assert.Equal(TableSaveKind.Saved, persistence.Status().Kind);
        }

        using WebApplicationFactory<Program> after = Host(connection, sessionId);
        var restoredSession = after.Services.GetRequiredService<SessionState>();
        var restoredBattle = after.Services.GetRequiredService<BattleState>();

        // Resolving the singletons is not what restores them — the hosted service already did, before
        // the host began serving. Reading them here is reading what the DM's first page load would see.
        Assert.Equal(SamplePack.MapId, restoredSession.CurrentMapId);
        Assert.Equal(SamplePack.MapId, restoredSession.SeededMapId);
        Assert.Equal("region-landing", Assert.Single(restoredSession.RevealedRegionIds()));
        Assert.Equal("trap-pit-1", Assert.Single(restoredSession.RevealedFeatureIds()));
        Assert.Equal("door-7", Assert.Single(restoredSession.OpenDoorIds()));
        Assert.Equal((10, 12), Assert.Single(restoredSession.FogCells()));

        var restoredCampaign = after.Services.GetRequiredService<CampaignState>();
        Assert.Equal(QuestStatus.Active, restoredCampaign.Status("the-millers-son"));
        Assert.True(restoredCampaign.IsBeatDone("the-millers-son", "find-tam"));
        Assert.Equal(QuestStatus.Complete, restoredCampaign.Status("the-lost-hymnal"));
        Assert.True(restoredCampaign.IsCardDrawn("village-rumours", "the-millers-debt"));

        Assert.True(restoredBattle.IsActive);
        Battle fight = restoredBattle.Snapshot();
        Assert.Equal("Bell-Founder's Workshop", fight.AreaTitle);
        Assert.True(fight.Started);
        Assert.Equal(17, fight.Entries[0].Initiative);

        // Counted by creature type rather than in total: StartBattle also seeds a row per member of the
        // committed party roster, and this test must not break when the DM adds a player to it.
        Assert.Equal(3, Bugbears(fight).Length);

        Combatant hurt = fight.Combatants.Single(combatant => string.Equals(combatant.Id, bugbearId, StringComparison.Ordinal));
        Assert.Equal("Bugbear 2", hurt.Name);
        Assert.Equal(7, hurt.CurrentHp);
        Assert.True(hurt.Bloodied);
        Assert.Equal(ConditionKind.Prone, Assert.Single(hurt.Conditions));

        // And the fight can still be *played*: the next bugbear is the fourth, not a second "Bugbear 1".
        restoredBattle.AddGroup(BugbearNode, "Bugbear", 1);
        Battle grown = restoredBattle.Snapshot();
        Assert.Equal(4, Bugbears(grown).Length);
        Assert.Contains("Bugbear 4", Bugbears(grown).Select(combatant => combatant.Name));
        Assert.Equal(
            grown.Combatants.Count,
            grown.Combatants.Select(combatant => combatant.Id).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task Ending_a_battle_leaves_nothing_to_restore()
    {
        string connection = TestPostgres.RequireConnection();
        string sessionId = $"test-ended-{Guid.NewGuid():N}";

        using (WebApplicationFactory<Program> before = Host(connection, sessionId))
        {
            var battle = before.Services.GetRequiredService<BattleState>();
            var persistence = before.Services.GetRequiredService<TablePersistence>();

            battle.AddGroup(BugbearNode, "Bugbear", 2);
            await persistence.FlushAsync(CancellationToken.None);
            battle.EndBattle();
            Assert.True(await persistence.FlushAsync(CancellationToken.None));
        }

        using WebApplicationFactory<Program> after = Host(connection, sessionId);

        Assert.False(after.Services.GetRequiredService<BattleState>().IsActive);
    }

    private static Combatant[] Bugbears(Battle battle) => battle.Combatants
        .Where(combatant => string.Equals(combatant.StatBlockNodeId, BugbearNode, StringComparison.Ordinal))
        .OrderBy(combatant => combatant.Ordinal)
        .ToArray();

    // The real host, configured exactly as production is bar the content, the connection string and
    // the session id.
    private static WebApplicationFactory<Program> Host(string connection, string sessionId) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            SamplePack.Serve(builder);
            builder.UseSetting("ConnectionStrings:Postgres", connection);
            builder.UseSetting("Session:Id", sessionId);
            builder.UseSetting("Auth:Passphrase", "test-passphrase");
        });
}
