using System.Collections.Generic;
using DungeonTable.Core.Battle;
using DungeonTable.Core.Stats;
using DungeonTable.Tests.Web;
using DungeonTable.Web.Services;

namespace DungeonTable.Tests.Battles;

/// <summary>
/// Verifies the rules the Battle tab leans on: stable per-type numbering, pre-fill from the stat
/// library (and graceful degradation without it), damage clamping, a turn order that skips downed
/// combatants, and a teardown that really tears down.
/// </summary>
public sealed class BattleStateTests
{
    private const string BugbearNode = "data_statblocks_monsters_bugbear";

    // Expected row orders, hoisted out of the assertions themselves (CA1861).
    private static readonly string[] SortedByInitiative = { "Fast", "Slow", "Unrolled" };
    private static readonly string[] TieBroken = { "Nimble", "First", "Second" };
    private static readonly string[] SplitBugbears = { "Bugbear 1", "Bugbear 2", "Bugbear 3" };
    private static readonly string[] Reordered = { "Third", "First", "Second" };

    [Fact]
    public void A_fresh_state_is_inactive_and_snapshots_empty()
    {
        BattleState state = Build();

        Battle snapshot = state.Snapshot();

        Assert.False(state.IsActive);
        Assert.False(snapshot.IsActive);
        Assert.Empty(snapshot.Entries);
        Assert.Empty(snapshot.Combatants);
    }

    [Fact]
    public void Starting_a_battle_seeds_one_row_per_party_member_with_their_numbers()
    {
        var party = new FakePartyRoster();
        party.Members.Add(new PartyMember { Name = "Rurik", MaxHp = 38, ArmourClass = 18, PassivePerception = 14, InitiativeModifier = 1 });
        party.Members.Add(new PartyMember { Name = "Sora", MaxHp = 24, ArmourClass = 15, PassivePerception = 17 });
        BattleState state = Build(party);

        state.StartBattle("data_dossiers_level_1_area_3a", "Nave");

        Battle snapshot = state.Snapshot();
        Assert.True(state.IsActive);
        Assert.Equal("data_dossiers_level_1_area_3a", snapshot.AreaNodeId);
        Assert.Equal("Nave", snapshot.AreaTitle);
        Assert.Equal(1, snapshot.Round);
        Assert.Equal(2, snapshot.Entries.Count);

        Combatant rurik = Find(snapshot, "Rurik");
        Assert.Equal(CombatantKind.Player, rurik.Kind);
        Assert.Equal(38, rurik.MaxHp);
        Assert.Equal(38, rurik.CurrentHp);
        Assert.Equal(18, rurik.ArmourClass);
        Assert.Equal(14, rurik.PassivePerception);

        // Initiative is what the players call out, so it starts blank rather than at zero.
        Assert.All(snapshot.Entries, entry => Assert.False(entry.HasInitiative));
    }

    [Fact]
    public void A_party_member_without_a_name_is_not_seeded()
    {
        var party = new FakePartyRoster();
        party.Members.Add(new PartyMember { Name = "   " });
        party.Members.Add(new PartyMember { Name = "Rurik" });
        BattleState state = Build(party);

        state.StartBattle("area", "Area");

        Assert.Single(state.Snapshot().Combatants);
    }

    [Fact]
    public void An_unreadable_party_roster_still_starts_a_battle()
    {
        var party = new FakePartyRoster { Broken = true };
        BattleState state = Build(party);

        state.StartBattle("area", "Area");

        Assert.True(state.IsActive);
        Assert.Empty(state.Snapshot().Combatants);
    }

    [Fact]
    public void Adding_a_group_numbers_its_members_and_fills_them_from_the_stat_block()
    {
        BattleState state = Build();
        state.StartBattle("area", "Area");

        state.AddGroup(BugbearNode, "Bugbear", 2);

        Battle snapshot = state.Snapshot();
        InitiativeEntry row = Assert.Single(snapshot.Entries);
        Assert.Equal("Bugbears", row.Label);
        Assert.Equal(CombatantKind.Monster, row.Kind);
        Assert.Equal(2, row.MemberIds.Count);

        // DEX 14 -> +2, so the in-app roll adds the right modifier without the DM looking it up.
        Assert.Equal(2, row.InitiativeModifier);

        Combatant first = Find(snapshot, "Bugbear 1");
        Assert.Equal(27, first.MaxHp);
        Assert.Equal(27, first.CurrentHp);
        Assert.Equal(16, first.ArmourClass);
        Assert.Equal(BugbearNode, first.StatBlockNodeId);
        Assert.NotNull(Find(snapshot, "Bugbear 2"));
    }

    [Fact]
    public void Adding_more_of_a_type_extends_the_same_row_and_continues_the_numbering()
    {
        BattleState state = Build();
        state.AddGroup(BugbearNode, "Bugbear", 2);

        state.AddGroup(BugbearNode, "Bugbear", 2);

        Battle snapshot = state.Snapshot();
        InitiativeEntry row = Assert.Single(snapshot.Entries);
        Assert.Equal(4, row.MemberIds.Count);
        Assert.NotNull(Find(snapshot, "Bugbear 3"));
        Assert.NotNull(Find(snapshot, "Bugbear 4"));
    }

    [Fact]
    public void Removing_a_member_never_renumbers_the_others()
    {
        BattleState state = Build();
        state.AddGroup(BugbearNode, "Bugbear", 3);
        string second = Find(state.Snapshot(), "Bugbear 2").Id;

        state.RemoveCombatant(second);
        state.AddGroup(BugbearNode, "Bugbear", 1);

        Battle snapshot = state.Snapshot();
        Assert.NotNull(Find(snapshot, "Bugbear 1"));
        Assert.Null(FindOrNull(snapshot, "Bugbear 2"));
        Assert.NotNull(Find(snapshot, "Bugbear 3"));
        Assert.NotNull(Find(snapshot, "Bugbear 4"));
    }

    [Fact]
    public void A_monster_with_no_stat_block_still_joins_the_fight()
    {
        BattleState state = Build();

        state.AddGroup("data_statblocks_monsters_unextracted", "Mystery Beast", 1);

        Battle snapshot = state.Snapshot();
        Combatant beast = Find(snapshot, "Mystery Beast 1");
        Assert.Equal(0, beast.MaxHp);
        Assert.Equal(0, beast.ArmourClass);
        Assert.False(beast.Down); // an unknown maximum means "untracked", not "dead"
        Assert.Equal("data_statblocks_monsters_unextracted", beast.StatBlockNodeId);
    }

    [Fact]
    public void A_named_individual_keeps_its_name_on_its_own_row_with_its_blocks_numbers()
    {
        // Grask fights as a bugbear; a group add would call him "Bugbear 1".
        BattleState state = Build();
        state.AddGroup(BugbearNode, "Bugbear", 2);

        string id = state.AddIndividual(BugbearNode, "  Grukk the Chief ");

        Battle snapshot = state.Snapshot();
        Assert.Equal(2, snapshot.Entries.Count);
        Combatant grukk = Find(snapshot, "Grukk the Chief");
        Assert.Equal(id, grukk.Id);
        Assert.Equal(27, grukk.MaxHp);
        Assert.Equal(16, grukk.ArmourClass);
        Assert.Equal(BugbearNode, grukk.StatBlockNodeId);
        Assert.Equal(CombatantKind.Monster, grukk.Kind);
        Assert.Equal(2, snapshot.Entries.Single(entry => entry.MemberIds.Contains(id)).InitiativeModifier);
        Assert.Equal(2, snapshot.Entries.Single(entry => !entry.MemberIds.Contains(id)).MemberIds.Count);
    }

    [Fact]
    public void An_encounter_row_adds_a_group_for_a_kind_and_named_rows_for_an_individual()
    {
        BattleState state = Build();

        state.AddEncounter(new EncounterMonster { NodeId = BugbearNode, StatBlockNodeId = BugbearNode, Name = "Bugbear" }, 2);
        state.AddEncounter(new EncounterMonster { NodeId = "grukk", StatBlockNodeId = BugbearNode, Name = "Grukk", Individual = true }, 1);
        state.AddEncounter(new EncounterMonster { NodeId = "twin", StatBlockNodeId = BugbearNode, Name = "A twin", Individual = true }, 2);
        state.AddEncounter(new EncounterMonster { NodeId = "none", StatBlockNodeId = BugbearNode, Name = "Nobody", Individual = true }, 0);

        Battle snapshot = state.Snapshot();
        Assert.Equal(4, snapshot.Entries.Count);
        Assert.Equal(2, Assert.Single(snapshot.Entries, entry => entry.Label == "Bugbears").MemberIds.Count);
        Assert.Equal(27, Find(snapshot, "Grukk").MaxHp);
        Assert.Equal(2, snapshot.Combatants.Count(combatant => combatant.Name == "A twin"));
        Assert.Null(FindOrNull(snapshot, "Nobody"));
    }

    [Fact]
    public void An_individual_with_no_stat_block_or_no_name_is_handled()
    {
        BattleState state = Build();

        Assert.NotEqual(string.Empty, state.AddIndividual(string.Empty, "Wenna Brask"));
        Assert.Equal(string.Empty, state.AddIndividual(BugbearNode, "   "));

        Combatant wenna = Assert.Single(state.Snapshot().Combatants);
        Assert.Equal("Wenna Brask", wenna.Name);
        Assert.Equal(0, wenna.MaxHp);
    }

    [Fact]
    public void Adding_a_group_outside_a_battle_opens_one()
    {
        BattleState state = Build();

        state.AddGroup(BugbearNode, "Bugbear", 1);

        Assert.True(state.IsActive);
    }

    [Fact]
    public void Two_unstatted_creatures_with_the_same_name_share_a_group()
    {
        BattleState state = Build();

        state.AddGroup(string.Empty, "Cultist", 1);
        state.AddGroup(string.Empty, "Cultist", 1);

        InitiativeEntry row = Assert.Single(state.Snapshot().Entries);
        Assert.Equal(2, row.MemberIds.Count);
    }

    [Fact]
    public void Damage_clamps_at_zero_and_marks_the_combatant_down()
    {
        BattleState state = Build();
        state.AddGroup(BugbearNode, "Bugbear", 1);
        string id = Find(state.Snapshot(), "Bugbear 1").Id;

        state.ApplyDamage(id, 100);

        Combatant hurt = Find(state.Snapshot(), "Bugbear 1");
        Assert.Equal(0, hurt.CurrentHp);
        Assert.True(hurt.Down);
    }

    [Fact]
    public void Healing_clamps_at_the_maximum_and_clears_down()
    {
        BattleState state = Build();
        state.AddGroup(BugbearNode, "Bugbear", 1);
        string id = Find(state.Snapshot(), "Bugbear 1").Id;
        state.ApplyDamage(id, 100);

        state.Heal(id, 500);

        Combatant healed = Find(state.Snapshot(), "Bugbear 1");
        Assert.Equal(27, healed.CurrentHp);
        Assert.False(healed.Down);
    }

    [Fact]
    public void Half_hit_points_or_fewer_reads_as_bloodied()
    {
        BattleState state = Build();
        state.AddGroup(BugbearNode, "Bugbear", 1);
        string id = Find(state.Snapshot(), "Bugbear 1").Id;

        state.SetHp(id, 14);
        Assert.False(Find(state.Snapshot(), "Bugbear 1").Bloodied);

        state.SetHp(id, 13);
        Assert.True(Find(state.Snapshot(), "Bugbear 1").Bloodied);
    }

    [Fact]
    public void Lowering_the_maximum_pulls_current_hit_points_down_with_it()
    {
        BattleState state = Build();
        state.AddGroup(BugbearNode, "Bugbear", 1);
        string id = Find(state.Snapshot(), "Bugbear 1").Id;

        state.SetMaxHp(id, 10);

        Assert.Equal(10, Find(state.Snapshot(), "Bugbear 1").CurrentHp);
    }

    [Fact]
    public void Initiative_is_set_blanked_and_rolled_within_the_die_range()
    {
        BattleState state = Build();
        state.AddGroup(BugbearNode, "Bugbear", 1);
        string entry = state.Snapshot().Entries[0].Id;

        state.SetInitiative(entry, 17);
        Assert.Equal(17, state.Snapshot().Entries[0].Initiative);
        Assert.True(state.Snapshot().Entries[0].HasInitiative);

        state.ClearInitiative(entry);
        Assert.False(state.Snapshot().Entries[0].HasInitiative);

        state.RollInitiative(entry);
        InitiativeEntry rolled = state.Snapshot().Entries[0];
        Assert.True(rolled.HasInitiative);
        Assert.InRange(rolled.Initiative, 1 + rolled.InitiativeModifier, 20 + rolled.InitiativeModifier);
    }

    [Fact]
    public void Rolling_all_monsters_leaves_players_blank()
    {
        var party = new FakePartyRoster();
        party.Members.Add(new PartyMember { Name = "Rurik" });
        BattleState state = Build(party);
        state.StartBattle("area", "Area");
        state.AddGroup(BugbearNode, "Bugbear", 1);

        state.RollAllMonsterInitiative();

        Battle snapshot = state.Snapshot();
        Assert.False(snapshot.Entries.Single(e => e.Kind == CombatantKind.Player).HasInitiative);
        Assert.True(snapshot.Entries.Single(e => e.Kind == CombatantKind.Monster).HasInitiative);
    }

    [Fact]
    public void Sorting_puts_the_highest_first_and_blanks_last()
    {
        BattleState state = Build();
        string slow = state.AddCombatant(Seed("Slow"));
        string fast = state.AddCombatant(Seed("Fast"));
        state.AddCombatant(Seed("Unrolled"));

        state.SetInitiative(EntryOf(state, slow), 8);
        state.SetInitiative(EntryOf(state, fast), 21);
        state.Sort();

        IReadOnlyList<InitiativeEntry> order = state.Snapshot().Entries;
        Assert.Equal(SortedByInitiative, order.Select(entry => entry.Label));
    }

    [Fact]
    public void Sorting_breaks_ties_by_modifier_then_by_the_order_rows_were_added()
    {
        BattleState state = Build();
        string first = state.AddCombatant(Seed("First"));
        string nimble = state.AddCombatant(Seed("Nimble", modifier: 4));
        string second = state.AddCombatant(Seed("Second"));

        foreach (string id in new[] { first, nimble, second })
        {
            state.SetInitiative(EntryOf(state, id), 12);
        }

        state.Sort();

        Assert.Equal(TieBroken, state.Snapshot().Entries.Select(entry => entry.Label));
    }

    [Fact]
    public void Sorting_keeps_the_turn_on_whoever_had_it()
    {
        BattleState state = Build();
        string slow = state.AddCombatant(Seed("Slow"));
        state.AddCombatant(Seed("Fast"));
        state.NextTurn(); // the fight starts on "Slow", the top row

        state.SetInitiative(EntryOf(state, slow), 3);
        state.SetInitiative(EntryOf(state, state.Snapshot().Combatants.Single(c => c.Name == "Fast").Id), 20);
        state.Sort();

        Battle snapshot = state.Snapshot();
        Assert.Equal("Slow", snapshot.Entries[snapshot.TurnIndex].Label);
    }

    [Fact]
    public void Moving_a_row_drops_it_where_it_was_put_without_touching_its_initiative()
    {
        BattleState state = Build();
        state.AddCombatant(Seed("First"));
        state.AddCombatant(Seed("Second"));
        string third = state.AddCombatant(Seed("Third"));
        state.SetInitiative(EntryOf(state, third), 12);

        state.MoveEntry(EntryOf(state, third), 0);

        IReadOnlyList<InitiativeEntry> order = state.Snapshot().Entries;
        Assert.Equal(Reordered, order.Select(entry => entry.Label));
        Assert.Equal(12, order[0].Initiative);
    }

    [Fact]
    public void Moving_a_row_keeps_the_turn_on_whoever_had_it()
    {
        BattleState state = Build();
        state.AddCombatant(Seed("First"));
        string second = state.AddCombatant(Seed("Second"));
        state.NextTurn(); // the fight starts on "First", the top row

        state.MoveEntry(EntryOf(state, second), 0);

        Battle snapshot = state.Snapshot();
        Assert.Equal("First", snapshot.Entries[snapshot.TurnIndex].Label);
    }

    [Fact]
    public void Moving_a_row_past_the_end_of_the_list_clamps_rather_than_throwing()
    {
        BattleState state = Build();
        string first = state.AddCombatant(Seed("First"));
        state.AddCombatant(Seed("Second"));

        state.MoveEntry(EntryOf(state, first), 99);

        Assert.Equal("First", state.Snapshot().Entries[^1].Label);
    }

    [Fact]
    public void Splitting_a_group_gives_each_member_its_own_row_carrying_the_group_initiative()
    {
        BattleState state = Build();
        state.AddGroup(BugbearNode, "Bugbear", 3);
        string group = state.Snapshot().Entries[0].Id;
        state.SetInitiative(group, 15);

        state.SplitGroup(group);

        IReadOnlyList<InitiativeEntry> rows = state.Snapshot().Entries;
        Assert.Equal(3, rows.Count);
        Assert.Equal(SplitBugbears, rows.Select(entry => entry.Label));
        Assert.All(rows, entry => Assert.Equal(15, entry.Initiative));
    }

    [Fact]
    public void Adding_more_after_a_split_opens_a_new_row_rather_than_guessing()
    {
        BattleState state = Build();
        state.AddGroup(BugbearNode, "Bugbear", 2);
        state.SplitGroup(state.Snapshot().Entries[0].Id);

        state.AddGroup(BugbearNode, "Bugbear", 1);

        Assert.Equal(3, state.Snapshot().Entries.Count);
    }

    [Fact]
    public void Merging_puts_a_split_group_back_on_one_row()
    {
        BattleState state = Build();
        state.AddGroup(BugbearNode, "Bugbear", 3);
        state.SplitGroup(state.Snapshot().Entries[0].Id);

        state.MergeGroup(state.Snapshot().Entries[0].Id);

        InitiativeEntry row = Assert.Single(state.Snapshot().Entries);
        Assert.Equal(3, row.MemberIds.Count);
        Assert.Equal("Bugbears", row.Label);
    }

    [Fact]
    public void A_group_added_after_a_named_individual_on_its_block_gets_a_row_of_its_own()
    {
        // Grask fights as a bugbear, but the bugbears who come in after him are not "Grask ×3".
        BattleState state = Build();
        string grask = state.AddIndividual(BugbearNode, "Grask");

        state.AddGroup(BugbearNode, "Bugbear", 2);

        Battle snapshot = state.Snapshot();
        Assert.Equal(2, snapshot.Entries.Count);
        InitiativeEntry his = snapshot.Entries.Single(entry => entry.MemberIds.Contains(grask));
        Assert.Equal("Grask", his.Label);
        Assert.Single(his.MemberIds);
        Assert.Equal(2, snapshot.Entries.Single(entry => entry.Label == "Bugbears").MemberIds.Count);
    }

    [Fact]
    public void More_of_a_type_still_join_its_group_when_an_ally_on_its_block_came_between()
    {
        BattleState state = Build();
        state.AddGroup(BugbearNode, "Bugbear", 2);
        state.AddCombatant(new CombatantSeed { Kind = CombatantKind.Ally, Name = "Wenna Brask", StatBlockNodeId = BugbearNode });

        state.AddGroup(BugbearNode, "Bugbear", 1);

        Battle snapshot = state.Snapshot();
        Assert.Equal(2, snapshot.Entries.Count);
        Assert.Equal(3, snapshot.Entries.Single(entry => entry.Label == "Bugbears").MemberIds.Count);
    }

    [Fact]
    public void Merging_leaves_a_named_individual_on_the_same_block_alone()
    {
        BattleState state = Build();
        state.AddGroup(BugbearNode, "Bugbear", 2);
        state.SplitGroup(state.Snapshot().Entries[0].Id);
        string grask = state.AddIndividual(BugbearNode, "Grask");
        string hisRow = state.Snapshot().Entries.Single(entry => entry.MemberIds.Contains(grask)).Id;
        string splitRow = state.Snapshot().Entries[0].Id;

        Assert.False(state.CanMerge(hisRow));
        Assert.True(state.CanMerge(splitRow));
        state.MergeGroup(hisRow);
        Assert.Equal(3, state.Snapshot().Entries.Count);

        state.MergeGroup(splitRow);

        Battle snapshot = state.Snapshot();
        Assert.Equal(2, snapshot.Entries.Count);
        Assert.Equal("Grask", snapshot.Entries.Single(entry => entry.MemberIds.Contains(grask)).Label);
        Assert.Equal(2, snapshot.Entries.Single(entry => !entry.MemberIds.Contains(grask)).MemberIds.Count);
    }

    [Fact]
    public void The_first_next_turn_starts_the_fight_on_the_top_row()
    {
        BattleState state = Build();
        state.AddCombatant(Seed("First"));
        state.AddCombatant(Seed("Second"));

        state.NextTurn();

        Battle snapshot = state.Snapshot();
        Assert.True(snapshot.Started);
        Assert.Equal(0, snapshot.TurnIndex);
        Assert.Equal(1, snapshot.Round);
    }

    [Fact]
    public void Wrapping_past_the_last_row_bumps_the_round()
    {
        BattleState state = Build();
        state.AddCombatant(Seed("First"));
        state.AddCombatant(Seed("Second"));

        state.NextTurn();
        state.NextTurn();
        state.NextTurn();

        Battle snapshot = state.Snapshot();
        Assert.Equal(0, snapshot.TurnIndex);
        Assert.Equal(2, snapshot.Round);
    }

    [Fact]
    public void The_turn_order_skips_a_row_whose_combatants_are_all_down()
    {
        BattleState state = Build();
        state.AddCombatant(Seed("First", maxHp: 10));
        string downed = state.AddCombatant(Seed("Downed", maxHp: 10));
        state.AddCombatant(Seed("Third", maxHp: 10));
        state.ApplyDamage(downed, 10);

        state.NextTurn();
        state.NextTurn();

        Assert.Equal("Third", CurrentLabel(state));
    }

    [Fact]
    public void Previous_turn_walks_back_and_unwinds_the_round()
    {
        BattleState state = Build();
        state.AddCombatant(Seed("First"));
        state.AddCombatant(Seed("Second"));
        state.NextTurn();

        state.PreviousTurn();

        Battle snapshot = state.Snapshot();
        Assert.Equal("Second", snapshot.Entries[snapshot.TurnIndex].Label);
        Assert.Equal(1, snapshot.Round);
    }

    [Fact]
    public void Removing_the_row_whose_turn_it_is_keeps_the_index_valid()
    {
        BattleState state = Build();
        state.AddCombatant(Seed("First"));
        state.AddCombatant(Seed("Second"));
        state.NextTurn();
        state.NextTurn();

        state.RemoveEntry(state.Snapshot().Entries[1].Id);

        Battle snapshot = state.Snapshot();
        Assert.Single(snapshot.Entries);
        Assert.Equal(0, snapshot.TurnIndex);
    }

    [Fact]
    public void Removing_a_row_above_the_turn_keeps_the_turn_on_whoever_had_it()
    {
        BattleState state = Build();
        state.AddCombatant(Seed("First"));
        state.AddCombatant(Seed("Second"));
        state.AddCombatant(Seed("Third"));
        state.NextTurn();
        state.NextTurn(); // the fight is on "Second"

        state.RemoveEntry(state.Snapshot().Entries[0].Id);

        Assert.Equal("Second", CurrentLabel(state));
    }

    [Fact]
    public void Removing_a_combatant_above_the_turn_keeps_the_turn_on_whoever_had_it()
    {
        BattleState state = Build();
        string first = state.AddCombatant(Seed("First"));
        state.AddCombatant(Seed("Second"));
        state.AddCombatant(Seed("Third"));
        state.NextTurn();
        state.NextTurn(); // the fight is on "Second"

        state.RemoveCombatant(first);

        Assert.Equal("Second", CurrentLabel(state));
    }

    [Fact]
    public void Conditions_toggle_on_and_off()
    {
        BattleState state = Build();
        string id = state.AddCombatant(Seed("Rurik"));

        state.ToggleCondition(id, ConditionKind.Prone);
        Assert.Contains(ConditionKind.Prone, Find(state.Snapshot(), "Rurik").Conditions);

        state.ToggleCondition(id, ConditionKind.Prone);
        Assert.Empty(Find(state.Snapshot(), "Rurik").Conditions);
    }

    [Fact]
    public void Concentration_records_what_it_is_on_and_clears_the_note_when_it_drops()
    {
        BattleState state = Build();
        string id = state.AddCombatant(Seed("Sora"));

        state.SetConcentration(id, true, "hold person");
        Assert.Equal("hold person", Find(state.Snapshot(), "Sora").ConcentrationNote);

        state.SetConcentration(id, false, "hold person");
        Combatant clear = Find(state.Snapshot(), "Sora");
        Assert.False(clear.Concentrating);
        Assert.Equal(string.Empty, clear.ConcentrationNote);
    }

    [Fact]
    public void Spending_and_restoring_slots_tracks_a_level_and_drops_it_at_zero()
    {
        BattleState state = Build();
        string id = state.AddCombatant(Seed("Sora"));

        state.SpendSlot(id, 2);
        state.SpendSlot(id, 2);
        Assert.Equal(2, Find(state.Snapshot(), "Sora").SpentSlots.Single(slot => slot.Level == 2).Spent);

        state.RestoreSlot(id, 2);
        state.RestoreSlot(id, 2);
        Assert.Empty(Find(state.Snapshot(), "Sora").SpentSlots);
    }

    [Fact]
    public void Ending_a_battle_clears_everything_and_the_next_one_starts_clean()
    {
        BattleState state = Build();
        state.AddGroup(BugbearNode, "Bugbear", 2);
        string id = Find(state.Snapshot(), "Bugbear 1").Id;
        state.ToggleCondition(id, ConditionKind.Prone);
        state.NextTurn();

        state.EndBattle();

        Assert.False(state.IsActive);
        Battle empty = state.Snapshot();
        Assert.Empty(empty.Entries);
        Assert.Empty(empty.Combatants);
        Assert.Equal(1, empty.Round);
        Assert.False(empty.Started);

        // Numbering restarts with the new fight rather than carrying the old battle's tally over.
        state.AddGroup(BugbearNode, "Bugbear", 1);
        Assert.NotNull(Find(state.Snapshot(), "Bugbear 1"));
    }

    [Fact]
    public void Starting_over_a_running_battle_replaces_it()
    {
        BattleState state = Build();
        state.StartBattle("first", "First");
        state.AddGroup(BugbearNode, "Bugbear", 2);

        state.StartBattle("second", "Second");

        Battle snapshot = state.Snapshot();
        Assert.Equal("second", snapshot.AreaNodeId);
        Assert.Empty(snapshot.Combatants);
    }

    [Fact]
    public void A_snapshot_is_a_copy_that_a_later_change_does_not_reach()
    {
        BattleState state = Build();
        state.AddGroup(BugbearNode, "Bugbear", 1);
        Battle before = state.Snapshot();

        state.ApplyDamage(before.Combatants[0].Id, 5);

        Assert.Equal(27, before.Combatants[0].CurrentHp);
        Assert.Equal(22, state.Snapshot().Combatants[0].CurrentHp);
    }

    [Fact]
    public void Every_mutation_raises_changed_once()
    {
        BattleState state = Build();
        int changes = 0;
        state.Changed += () => changes++;

        state.StartBattle("area", "Area");
        state.AddGroup(BugbearNode, "Bugbear", 1);
        state.EndBattle();

        Assert.Equal(3, changes);
    }

    [Fact]
    public void A_no_op_raises_nothing()
    {
        BattleState state = Build();
        state.AddGroup(BugbearNode, "Bugbear", 1);
        int changes = 0;
        state.Changed += () => changes++;

        state.AddGroup(BugbearNode, "Bugbear", 0);
        state.RemoveCombatant("no-such-combatant");
        state.RemoveEntry("no-such-entry");
        state.SetInitiative("no-such-entry", 10);
        state.ApplyDamage("no-such-combatant", 5);
        state.MoveEntry("no-such-entry", 0);
        state.MoveEntry(state.Snapshot().Entries[0].Id, 0);

        Assert.Equal(0, changes);
    }

    private static BattleState Build() => Build(new FakePartyRoster());

    private static BattleState Build(FakePartyRoster party)
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

        return new BattleState(party, stats);
    }

    private static CombatantSeed Seed(string name, int maxHp = 0, int modifier = 0) => new CombatantSeed
    {
        Kind = CombatantKind.Player,
        Name = name,
        MaxHp = maxHp,
        InitiativeModifier = modifier,
    };

    private static Combatant Find(Battle snapshot, string name) =>
        Assert.Single(snapshot.Combatants, combatant => string.Equals(combatant.Name, name, StringComparison.Ordinal));

    private static Combatant FindOrNull(Battle snapshot, string name) =>
        snapshot.Combatants.FirstOrDefault(combatant => string.Equals(combatant.Name, name, StringComparison.Ordinal));

    private static string EntryOf(BattleState state, string combatantId) =>
        state.Snapshot().Entries.Single(entry => entry.MemberIds.Contains(combatantId)).Id;

    private static string CurrentLabel(BattleState state)
    {
        Battle snapshot = state.Snapshot();
        return snapshot.Entries[snapshot.TurnIndex].Label;
    }
}
