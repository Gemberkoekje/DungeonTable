using DungeonTable.Core.Dossier;
using DungeonTable.Core.Stats;
using DungeonTable.Infrastructure.Links;
using DungeonTable.Tests.Web;
using DungeonTable.Web.Services;

namespace DungeonTable.Tests.Battles;

/// <summary>
/// Verifies which of an area's dossier blocks reach the combatant detail pane: the ones that name the
/// creature, by stat-block node id or by its own name, in the order the book prints them — and none of
/// the ones that do not, since a pane that shows the whole room is a pane the DM stops reading.
/// </summary>
public sealed class EncounterContextTests
{
    private const string Area = "data_dossiers_level_1_area_2b";
    private const string Bugbear = "data_statblocks_monsters_bugbear";
    private const string Goblin = "data_statblocks_monsters_goblin";

    [Fact]
    public void An_area_with_no_dossier_yields_nothing()
    {
        EncounterContext context = Build(new FakeDossierStore());

        Assert.Empty(context.For(Area, Bugbear, "Bugbear 1"));
    }

    [Fact]
    public void A_blank_area_id_yields_nothing()
    {
        EncounterContext context = Build(Store(new AreaDossier { ReadAloud = "Two bugbears wait." }));

        Assert.Empty(context.For(string.Empty, Bugbear, "Bugbear 1"));
    }

    [Fact]
    public void The_read_aloud_that_names_the_creature_comes_through_as_the_read_aloud_note()
    {
        EncounterContext context = Build(Store(new AreaDossier
        {
            ReadAloud = "Two bugbears crouch behind the pillars.",
        }));

        EncounterNote note = Assert.Single(context.For(Area, Bugbear, "Bugbear 1"));

        Assert.Equal("read-aloud", note.Section);
        Assert.Equal(string.Empty, note.Heading);
        Assert.Contains("Two bugbears", note.Body, StringComparison.Ordinal);
        Assert.False(note.Secret);
    }

    [Fact]
    public void A_note_shows_the_words_of_its_links_not_their_markup()
    {
        FakeDossierStore store = Store(new AreaDossier
        {
            Detail = new[]
            {
                new DossierBlock { Heading = "Tactics", Body = "Two [[bugbear|bugbears]] fall back to [[area 6d]]." },
            },
        });
        var stats = new FakeStatLibrary();
        stats.Monsters[Bugbear] = new StatBlock { NodeId = Bugbear, Name = "Bugbear" };
        var context = new EncounterContext(
            store,
            new MarkupCrossRefResolver(LinkTargets.Build(store, stats)));

        EncounterNote note = Assert.Single(context.For(Area, Bugbear, "Bugbear 1"));

        // The area link names no floor this pane knows of, so it is broken here; its words still show.
        Assert.Equal("Two bugbears fall back to area 6d.", note.Body);
    }

    [Fact]
    public void A_detail_block_naming_the_creature_keeps_its_heading_and_secret_flag()
    {
        EncounterContext context = Build(Store(new AreaDossier
        {
            Detail = new[]
            {
                new DossierBlock { Heading = "Bugbear Tactics", Body = "They attack from cover.", Secret = true },
            },
        }));

        EncounterNote note = Assert.Single(context.For(Area, Bugbear, "Bugbear 1"));

        Assert.Equal("detail", note.Section);
        Assert.Equal("Bugbear Tactics", note.Heading);
        Assert.True(note.Secret);
    }

    [Fact]
    public void A_block_that_says_nothing_about_this_creature_is_left_out()
    {
        EncounterContext context = Build(Store(new AreaDossier
        {
            ReadAloud = "Two bugbears crouch behind the pillars.",
            Glance = new[] { new DossierBlock { Heading = "Sandy Floor", Body = "Footprints show in the dust." } },
        }));

        EncounterNote note = Assert.Single(context.For(Area, Bugbear, "Bugbear 1"));

        Assert.Equal("read-aloud", note.Section);
    }

    [Fact]
    public void Blocks_come_back_in_the_order_the_book_prints_them()
    {
        EncounterContext context = Build(Store(new AreaDossier
        {
            ReadAloud = "Bugbears lurk here.",
            Glance = new[] { new DossierBlock { Heading = "Bugbears", Body = "Four of them." } },
            Detail = new[] { new DossierBlock { Heading = "Tactics", Body = "The bugbears use reach." } },
        }));

        var notes = context.For(Area, Bugbear, "Bugbear 1");

        Assert.Equal(3, notes.Count);
        Assert.Equal("read-aloud", notes[0].Section);
        Assert.Equal("at a glance", notes[1].Section);
        Assert.Equal("detail", notes[2].Section);
    }

    [Fact]
    public void A_node_id_match_finds_a_block_that_never_spells_the_name_out()
    {
        // The resolver links "the pack" to the bugbear node; nothing in the text says "bugbear".
        var resolver = new ScriptedCrossRefResolver().Add("the pack", Bugbear, CrossRefKind.Monster);
        EncounterContext context = new EncounterContext(
            Store(new AreaDossier
            {
                Detail = new[] { new DossierBlock { Heading = "The Pack", Body = "The pack fights as one." } },
            }),
            resolver);

        EncounterNote note = Assert.Single(context.For(Area, Bugbear, "Bugbear 1"));

        Assert.Equal("The Pack", note.Heading);
    }

    [Fact]
    public void A_numbered_and_renamed_combatant_still_finds_its_own_creature()
    {
        EncounterContext context = Build(Store(new AreaDossier { ReadAloud = "Four bugbears wait in ambush." }));

        Assert.Single(context.For(Area, Bugbear, "Bugbear 3 (captain)"));
    }

    [Fact]
    public void A_combatant_with_no_stat_block_still_matches_on_its_name()
    {
        EncounterContext context = Build(Store(new AreaDossier { ReadAloud = "Two bugbears wait in ambush." }));

        Assert.Single(context.For(Area, string.Empty, "Bugbear 1"));
    }

    [Fact]
    public void A_one_off_combatant_named_after_nothing_in_the_room_matches_nothing()
    {
        EncounterContext context = Build(Store(new AreaDossier { ReadAloud = "Two bugbears wait in ambush." }));

        Assert.Empty(context.For(Area, string.Empty, "Rurik"));
    }

    [Fact]
    public void Only_the_blocks_naming_this_creature_come_back_when_the_room_holds_several()
    {
        EncounterContext context = Build(Store(new AreaDossier
        {
            ReadAloud = "Two bugbears crouch behind the pillars.",
            Detail = new[]
            {
                new DossierBlock { Heading = "Goblin Camp", Body = "Three goblins argue over a pot." },
            },
        }));

        EncounterNote note = Assert.Single(context.For(Area, Goblin, "Goblin 1"));

        Assert.Equal("Goblin Camp", note.Heading);
    }

    [Fact]
    public void A_named_individual_finds_the_blocks_that_link_to_it_rather_than_to_its_stat_block()
    {
        // Grukk fights with a bugbear's numbers, but the prose links him as himself, by his first name
        // only. Neither the stat block nor his full name appears in the text.
        FakeDossierStore store = Store(new AreaDossier
        {
            Detail = new[]
            {
                new DossierBlock { Heading = "The Chief", Body = "[[grukk|The chief]] never fights alone." },
                new DossierBlock { Heading = "The Guards", Body = "His guards wear red." },
            },
        });
        store.Levels.Add(new LevelDossier
        {
            LevelNodeId = "data_dossiers_level_1",
            Entities = new[] { new Entity { Id = "grukk", Kind = EntityKind.Npc, Name = "Grukk the Chief", StatBlock = "bugbear" } },
        });
        var stats = new FakeStatLibrary();
        stats.Monsters[Bugbear] = new StatBlock { NodeId = Bugbear, Name = "Bugbear" };
        var context = new EncounterContext(
            store,
            new MarkupCrossRefResolver(LinkTargets.Build(store, stats)));

        EncounterNote note = Assert.Single(context.For(Area, Bugbear, "Grukk the Chief"));

        Assert.Equal("The Chief", note.Heading);
        Assert.Equal("The chief never fights alone.", note.Body);
    }

    [Fact]
    public void A_roster_npc_in_the_fight_finds_the_blocks_that_link_to_them()
    {
        FakeDossierStore store = Store(new AreaDossier { ReadAloud = "[[wenna-brask|The miller]] is here." });
        store.Npcs = new NpcRoster { Npcs = new[] { new NpcDossier { Id = "wenna-brask", Name = "Wenna Brask" } } };
        var context = new EncounterContext(
            store,
            new MarkupCrossRefResolver(LinkTargets.Build(store, new FakeStatLibrary())));

        Assert.Single(context.For(Area, string.Empty, "Wenna Brask"));
        Assert.Empty(context.For(Area, string.Empty, "Someone Else"));
    }

    private static FakeDossierStore Store(AreaDossier dossier)
    {
        var store = new FakeDossierStore();
        store.Areas[Area] = dossier;
        return store;
    }

    // The real resolver reads link markup; these tests only need it to link the creature's own name,
    // so the name route and the node-id route are exercised separately.
    private static EncounterContext Build(FakeDossierStore store) =>
        new EncounterContext(
            store,
            new ScriptedCrossRefResolver()
                .Add("bugbears", Bugbear, CrossRefKind.Monster)
                .Add("goblins", Goblin, CrossRefKind.Monster));
}
