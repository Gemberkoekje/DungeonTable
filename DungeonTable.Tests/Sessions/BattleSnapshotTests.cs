using System.Collections.Generic;
using DungeonTable.Core.Battle;
using DungeonTable.Core.Session;
using DungeonTable.Core.Stats;
using DungeonTable.Tests.Battles;
using DungeonTable.Tests.Web;
using DungeonTable.Web.Services;

namespace DungeonTable.Tests.Sessions;

/// <summary>
/// Verifies that a fight survives a capture/restore round trip in a state that can still be
/// <em>played</em> — not merely displayed. The counters are the substance of these tests: a restored
/// fight that re-issues an id already in use, or numbers a second "Bugbear 1", is worse than one that
/// was lost, because the DM would not notice until it mattered.
/// </summary>
public sealed class BattleSnapshotTests
{
    private const string BugbearNode = "data_statblocks_monsters_bugbear";

    private static readonly string[] ThreeBugbears = { "Bugbear 1", "Bugbear 2", "Bugbear 3" };
    private static readonly string[] BugbearsOneAndThree = { "Bugbear 1", "Bugbear 3" };

    [Fact]
    public void A_fight_in_progress_survives_a_capture_and_restore()
    {
        BattleState state = Build();
        state.StartBattle("data_dossiers_level_1_area_3a", "Nave");
        state.AddGroup(BugbearNode, "Bugbear", 3);
        Battle before = state.Snapshot();
        string bugbear = Find(before, "Bugbear 2").Id;
        state.ApplyDamage(bugbear, 20);
        state.ToggleCondition(bugbear, ConditionKind.Prone);
        state.SetNotes(bugbear, "fled east");
        state.SetConcentration(bugbear, concentrating: true, "hold person");
        state.SpendSlot(bugbear, 2);
        state.SetInitiative(before.Entries[0].Id, 17);
        state.NextTurn();

        BattleSnapshot stored = state.Capture();
        BattleState restored = Build();
        restored.Restore(stored);

        Battle after = restored.Snapshot();
        Assert.True(restored.IsActive);
        Assert.Equal("data_dossiers_level_1_area_3a", after.AreaNodeId);
        Assert.Equal("Nave", after.AreaTitle);
        Assert.Equal(1, after.Round);
        Assert.True(after.Started);
        Assert.Equal(before.Entries.Count, after.Entries.Count);
        Assert.Equal(17, after.Entries[0].Initiative);
        Assert.True(after.Entries[0].HasInitiative);
        Assert.Equal(ThreeBugbears, after.Combatants.OrderBy(c => c.Ordinal).Select(c => c.Name));

        Combatant hurt = Find(after, "Bugbear 2");
        Assert.Equal(7, hurt.CurrentHp);
        Assert.Equal(27, hurt.MaxHp);
        Assert.Equal(16, hurt.ArmourClass);
        Assert.True(hurt.Bloodied);
        Assert.Equal(ConditionKind.Prone, Assert.Single(hurt.Conditions));
        Assert.Equal("fled east", hurt.Notes);
        Assert.True(hurt.Concentrating);
        Assert.Equal("hold person", hurt.ConcentrationNote);
        Assert.Equal(2, Assert.Single(hurt.SpentSlots).Level);
        Assert.Equal(BugbearNode, hurt.StatBlockNodeId);
    }

    [Fact]
    public void Numbering_continues_after_a_restore_instead_of_starting_over()
    {
        BattleState state = Build();
        state.AddGroup(BugbearNode, "Bugbear", 2);

        BattleState restored = Build();
        restored.Restore(state.Capture());
        restored.AddGroup(BugbearNode, "Bugbear", 1);

        Battle after = restored.Snapshot();
        Assert.Equal(ThreeBugbears, after.Combatants.OrderBy(c => c.Ordinal).Select(c => c.Name));
    }

    [Fact]
    public void Ids_issued_after_a_restore_never_collide_with_the_restored_ones()
    {
        BattleState state = Build();
        state.AddGroup(BugbearNode, "Bugbear", 3);
        state.AddCombatant(Seed("Rurik"));

        BattleState restored = Build();
        restored.Restore(state.Capture());
        restored.AddCombatant(Seed("Sora"));

        Battle after = restored.Snapshot();
        Assert.Equal(
            after.Combatants.Count,
            after.Combatants.Select(combatant => combatant.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            after.Entries.Count,
            after.Entries.Select(entry => entry.Id).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void A_document_with_lost_counters_still_cannot_re_issue_an_id()
    {
        // A hand-edited document, or one written by a build that tracked counters differently: the ids
        // themselves are the fallback source of "how far the numbering got".
        BattleState state = Build();
        state.AddGroup(BugbearNode, "Bugbear", 2);
        BattleSnapshot stored = state.Capture();
        string[] storedIds = stored.Combatants.Select(combatant => combatant.Id).ToArray();
        stored.LastIdNumber = 0;
        stored.NextSequence = 0;

        BattleState restored = Build();
        restored.Restore(stored);
        restored.AddCombatant(Seed("Rurik"));

        Battle after = restored.Snapshot();
        Combatant added = Find(after, "Rurik");
        Assert.DoesNotContain(added.Id, storedIds);
    }

    [Fact]
    public void The_sort_tie_break_still_works_on_rows_added_after_a_restore()
    {
        // Sequence is the initiative sort's final tie-break. A restored row keeping sequence 0 while a
        // newly added row also got 0 would make equal rows sort arbitrarily.
        BattleState state = Build();
        state.AddCombatant(Seed("First", modifier: 0));
        state.AddCombatant(Seed("Second", modifier: 0));

        BattleState restored = Build();
        restored.Restore(state.Capture());
        restored.AddCombatant(Seed("Third", modifier: 0));

        Battle after = restored.Snapshot();
        Assert.Equal(
            after.Entries.Count,
            after.Entries.Select(entry => entry.Sequence).Distinct().Count());
    }

    [Fact]
    public void Restoring_an_empty_snapshot_leaves_no_fight_and_still_notifies()
    {
        BattleState state = Build();
        state.AddGroup(BugbearNode, "Bugbear", 2);
        int changes = 0;
        state.Changed += () => changes++;

        state.Restore(new BattleSnapshot());

        Assert.False(state.IsActive);
        Assert.Empty(state.Snapshot().Entries);
        Assert.Empty(state.Snapshot().Combatants);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void A_null_snapshot_leaves_the_fight_alone()
    {
        BattleState state = Build();
        state.AddGroup(BugbearNode, "Bugbear", 2);
        int changes = 0;
        state.Changed += () => changes++;

        state.Restore(null);

        Assert.True(state.IsActive);
        Assert.Equal(2, state.Snapshot().Combatants.Count);
        Assert.Equal(0, changes);
    }

    [Fact]
    public void A_row_whose_combatants_are_missing_from_the_document_is_dropped()
    {
        BattleState state = Build();
        state.AddGroup(BugbearNode, "Bugbear", 2);
        state.AddCombatant(Seed("Rurik"));
        BattleSnapshot stored = state.Capture();
        stored.Combatants = stored.Combatants
            .Where(combatant => !string.Equals(combatant.Name, "Rurik", StringComparison.Ordinal))
            .ToArray();

        BattleState restored = Build();
        restored.Restore(stored);

        Battle after = restored.Snapshot();
        Assert.Equal(2, after.Combatants.Count);
        Assert.Single(after.Entries);
        Assert.DoesNotContain("Rurik", after.Entries.Select(entry => entry.Label));
    }

    [Fact]
    public void A_group_row_keeps_the_members_that_are_present()
    {
        BattleState state = Build();
        state.AddGroup(BugbearNode, "Bugbear", 3);
        BattleSnapshot stored = state.Capture();
        stored.Combatants = stored.Combatants
            .Where(combatant => combatant.Ordinal != 2)
            .ToArray();

        BattleState restored = Build();
        restored.Restore(stored);

        Battle after = restored.Snapshot();
        InitiativeEntry row = Assert.Single(after.Entries);
        Assert.Equal(2, row.MemberIds.Count);
        Assert.Equal(BugbearsOneAndThree, after.Combatants.OrderBy(c => c.Ordinal).Select(c => c.Name));
    }

    [Fact]
    public void A_turn_index_past_the_end_of_the_order_is_clamped_back_into_it()
    {
        BattleState state = Build();
        state.AddGroup(BugbearNode, "Bugbear", 1);
        BattleSnapshot stored = state.Capture();
        stored.TurnIndex = 99;
        stored.Started = true;

        BattleState restored = Build();
        restored.Restore(stored);

        Assert.Equal(0, restored.Snapshot().TurnIndex);
    }

    [Fact]
    public void A_round_below_one_restores_as_round_one()
    {
        BattleState state = Build();
        state.AddGroup(BugbearNode, "Bugbear", 1);
        BattleSnapshot stored = state.Capture();
        stored.Round = 0;

        BattleState restored = Build();
        restored.Restore(stored);

        Assert.Equal(1, restored.Snapshot().Round);
    }

    [Fact]
    public void A_restored_fight_can_be_ended_and_a_new_one_started_cleanly()
    {
        BattleState state = Build();
        state.AddGroup(BugbearNode, "Bugbear", 2);

        BattleState restored = Build();
        restored.Restore(state.Capture());
        restored.EndBattle();

        Assert.False(restored.IsActive);
        Assert.Empty(restored.Capture().Combatants);
        Assert.Empty(restored.Capture().IssuedOrdinals);
    }

    [Fact]
    public void Capturing_an_inactive_battle_reports_no_battle()
    {
        BattleSnapshot stored = Build().Capture();

        Assert.False(stored.HasBattle());
        Assert.Empty(stored.Entries);
        Assert.Empty(stored.Combatants);
    }

    private static BattleState Build()
    {
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

        return new BattleState(new FakePartyRoster(), stats);
    }

    private static CombatantSeed Seed(string name, int modifier = 0) => new CombatantSeed
    {
        Kind = CombatantKind.Player,
        Name = name,
        InitiativeModifier = modifier,
    };

    private static Combatant Find(Battle battle, string name) =>
        battle.Combatants.Single(combatant => string.Equals(combatant.Name, name, StringComparison.Ordinal));
}
