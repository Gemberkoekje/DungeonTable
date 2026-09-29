using DungeonTable.Core.Dossier;
using DungeonTable.Core.Stats;
using DungeonTable.Infrastructure.Links;
using DungeonTable.Tests.Web;

namespace DungeonTable.Tests.Links;

/// <summary>
/// Every relation in the content, read from both ends: from level files, the campaign list and NPC
/// contacts; written twice, kept once; and never drawn half-way.
/// </summary>
public sealed class RelationIndexTests
{
    private const string Level1 = "data_dossiers_level_1";
    private const string Workshop = "data_dossiers_level_1_area_4";
    private const string Den = "data_dossiers_level_1_area_2";

    // Expected values, hoisted out of the assertions (CA1861).
    private static readonly string[] WorkshopKey = { "4" };
    private static readonly string[] WennaOnly = { "wenna-brask" };
    private static readonly string[] MotherAndSon = { "tam-brask", "wenna-brask" };

    [Fact]
    public void A_level_relation_reads_from_both_ends()
    {
        RelationIndex index = Build(new Relation { A = "nib", Rel = "rival", B = "grask", Note = "Over [[area 4|the bugbears' forge]]." });

        RelationLine fromNib = Assert.Single(index.For("nib"));
        Assert.Equal("rival", fromNib.Kind);
        Assert.Equal("rival of", fromNib.Phrase);
        Assert.Equal("grask", fromNib.OtherId);
        Assert.Equal("Grask", fromNib.OtherLabel);
        Assert.Equal(CrossRefKind.Npc, fromNib.OtherKind);
        Assert.Equal("Over [[area 4|the bugbears' forge]].", fromNib.Note);
        Assert.Equal(Level1, fromNib.Floor);
        Assert.Empty(fromNib.FromContactsOf);

        RelationLine fromGrask = Assert.Single(index.For("grask"));
        Assert.Equal("nib", fromGrask.OtherId);
        Assert.Equal("Nib", fromGrask.OtherLabel);
        Assert.Equal(1, index.Count);
    }

    [Fact]
    public void A_line_says_which_rooms_list_the_other_end()
    {
        // Grask is listed in his workshop and only mentioned in the den: only the workshop counts.
        RelationIndex index = Build(new Relation { A = "nib", Rel = "plots-against", B = "grask" });

        RelationLine line = Assert.Single(index.For("nib"));

        Assert.Equal("plots against", line.Phrase);
        Assert.Equal(WorkshopKey, line.OtherWhere);
        Assert.Equal("plotted against by", Assert.Single(index.For("grask")).Phrase);
        Assert.Empty(Assert.Single(index.For("grask")).OtherWhere);
    }

    [Fact]
    public void A_faction_block_an_area_and_a_campaign_relation_are_ends_too()
    {
        FakeDossierStore dossiers = Dossiers(new Relation { A = "wickfoot-goblins", Rel = "located-in", B = "area 2" });
        dossiers.Relations = new RelationList
        {
            Relations = new[] { new Relation { A = "wenna-brask", Rel = "fears", B = "L1 area 4", Secret = true } },
        };

        RelationIndex index = Index(dossiers);

        RelationLine home = Assert.Single(index.For(Den));
        Assert.Equal("home to", home.Phrase);
        Assert.Equal("Wickfoot goblins", home.OtherLabel);
        Assert.Equal(CrossRefKind.Faction, home.OtherKind);
        Assert.Equal("Goblin Den (Level 1, Area 2)", Assert.Single(index.For("wickfoot-goblins")).OtherLabel);

        RelationLine feared = Assert.Single(index.For(Workshop));
        Assert.Equal("feared by", feared.Phrase);
        Assert.True(feared.Secret);
        Assert.Equal(string.Empty, feared.Floor);
    }

    [Fact]
    public void A_contact_that_names_a_person_and_a_kind_is_a_relation_with_no_note()
    {
        FakeDossierStore dossiers = Dossiers();
        dossiers.Npcs = new NpcRoster
        {
            Npcs = new[]
            {
                Npc("wenna-brask", "Wenna Brask",
                    new CastMember { Name = "Tam", Role = "Her son.", Ref = "tam-brask", Rel = "protects" },
                    new CastMember { Name = "Old Hobb", Role = "A customer.", Ref = "tam-brask" },
                    new CastMember { Name = "Brother Aldous", Role = "Her priest." }),
                Npc("tam-brask", "Tam Brask"),
            },
        };

        RelationIndex index = Index(dossiers);

        RelationLine line = Assert.Single(index.For("tam-brask"));
        Assert.Equal("protected by", line.Phrase);
        Assert.Equal("Wenna Brask", line.OtherLabel);

        // The role is written about Tam from Wenna's side; on Tam's card it would misread.
        Assert.Equal(string.Empty, line.Note);
        Assert.Equal(WennaOnly, line.FromContactsOf);
        Assert.Equal(1, index.Count);
    }

    [Fact]
    public void A_relation_written_twice_is_one_known_while_either_copy_is_and_remembers_both_contacts()
    {
        FakeDossierStore dossiers = Dossiers();
        dossiers.Npcs = new NpcRoster
        {
            Npcs = new[]
            {
                Npc("tam-brask", "Tam Brask", new CastMember { Name = "Wenna", Ref = "wenna-brask", Rel = "family", Secret = true }),
                Npc("wenna-brask", "Wenna Brask", new CastMember { Name = "Tam", Ref = "tam-brask", Rel = "family" }),
            },
        };

        RelationIndex index = Index(dossiers);

        RelationLine line = Assert.Single(index.For("tam-brask"));
        Assert.False(line.Secret);
        Assert.Equal(MotherAndSon, line.FromContactsOf);
        Assert.Equal(1, index.Count);
    }

    [Fact]
    public void A_directed_kind_written_both_ways_is_two_relations()
    {
        RelationIndex index = Build(
            new Relation { A = "nib", Rel = "fears", B = "grask" },
            new Relation { A = "grask", Rel = "fears", B = "nib" },
            new Relation { A = "grask", Rel = "rival", B = "nib", Note = "first" },
            new Relation { A = "nib", Rel = "rival", B = "grask", Note = "second" });

        Assert.Equal(3, index.Count);
        Assert.Equal("first", index.For("nib").Single(line => line.Kind == "rival").Note);
    }

    [Theory]
    [InlineData("bandit")]
    [InlineData("detect-magic")]
    [InlineData("nobody-at-all")]
    [InlineData("L9 area 1")]
    [InlineData("nib")]
    public void A_relation_with_an_end_that_is_no_particular_thing_is_left_out(string end)
    {
        RelationIndex index = Build(new Relation { A = "nib", Rel = "hunts", B = end });

        Assert.Equal(0, index.Count);
        Assert.Empty(index.For("nib"));
        Assert.Empty(index.For(string.Empty));
    }

    [Fact]
    public void A_book_link_reads_its_verb_from_the_end_that_does_it_and_as_a_note_from_the_other()
    {
        RelationIndex index = Books(Link("the-old-mill", "wenna-brask", "owned and operated by"));

        RelationLine fromMill = Assert.Single(index.For("the-old-mill"));
        Assert.Equal(RelationKinds.Related, fromMill.Kind);
        Assert.Equal("owned and operated by", fromMill.Phrase);
        Assert.Equal(string.Empty, fromMill.Note);
        Assert.Equal("wenna-brask", fromMill.OtherId);
        Assert.Equal(CrossRefKind.Npc, fromMill.OtherKind);

        RelationLine fromWenna = Assert.Single(index.For("wenna-brask"));
        Assert.Equal("related to", fromWenna.Phrase);
        Assert.Equal("owned and operated by", fromWenna.Note);
        Assert.Equal("The Old Mill", fromWenna.OtherLabel);
        Assert.Equal(CrossRefKind.Book, fromWenna.OtherKind);
    }

    [Fact]
    public void Two_verbs_between_the_same_ends_are_two_book_links_and_one_verb_twice_is_one()
    {
        RelationIndex index = Books(
            Link("the-old-mill", "wickfoot-goblins", "robbed by"),
            Link("wickfoot-goblins", "the-old-mill", "steals from"),
            Link("the-old-mill", "wickfoot-goblins", "robbed by"));

        Assert.Equal(2, index.Count);
        Assert.Equal(2, index.For("the-old-mill").Count);
    }

    [Fact]
    public void A_book_link_may_end_at_a_stat_block_or_a_spell_where_an_authored_relation_may_not()
    {
        RelationIndex index = Books(
            Link("the-old-mill", "bandit", "full of"),
            Link("the-old-mill", "detect-magic", "warded against"));

        Assert.Equal(2, index.For("the-old-mill").Count);
        Assert.Contains(index.For("the-old-mill"), line => line.OtherId == "data_statblocks_monsters_bandit");
    }

    [Fact]
    public void A_book_link_with_an_end_nothing_answers_to_is_left_out()
    {
        Assert.Equal(0, Books(Link("the-old-mill", "nobody-at-all", "run by")).Count);
    }

    private static RelationIndex Build(params Relation[] relations) => Index(Dossiers(relations));

    // The fixture, with a book index that has the Old Mill and the given links.
    private static RelationIndex Books(params Relation[] links)
    {
        var book = new BookIndex
        {
            Book = "almanac",
            Entries = new[] { new BookEntry { Id = "the-old-mill", Kind = "lore", Name = "The Old Mill" } },
            Links = links,
        };

        return Index(Dossiers(), book);
    }

    private static Relation Link(string a, string b, string verb) =>
        new Relation { A = a, Rel = RelationKinds.Related, B = b, Note = verb };

    private static RelationIndex Index(FakeDossierStore dossiers, params BookIndex[] books)
    {
        var stats = new FakeStatLibrary();
        stats.Monsters["data_statblocks_monsters_bandit"] = new StatBlock { NodeId = "data_statblocks_monsters_bandit", Name = "Bandit" };
        stats.Spells["data_statblocks_spells_detect_magic"] = new SpellEntry { NodeId = "data_statblocks_spells_detect_magic", Name = "Detect Magic" };
        LinkTargets targets = LinkTargets.Build(dossiers, stats, books);
        return books.Length == 0
            ? RelationIndex.Build(dossiers, targets, Backlinks.Build(dossiers, targets))
            : RelationIndex.Build(dossiers, targets, Backlinks.Build(dossiers, targets), books);
    }

    // Level 1 with Nib, Grask and the Wickfoot goblins; Grask listed in his workshop and mentioned in
    // the den; Wenna in the NPC roster.
    private static FakeDossierStore Dossiers(params Relation[] relations)
    {
        var level = new LevelDossier
        {
            LevelNodeId = Level1,
            Entities = new[]
            {
                new Entity { Id = "nib", Kind = EntityKind.Npc, Name = "Nib" },
                new Entity { Id = "grask", Kind = EntityKind.Npc, Name = "Grask" },
            },
            Factions = new[] { new DossierBlock { Id = "wickfoot-goblins", Heading = "Wickfoot goblins" } },
            Relations = relations,
        };

        var dossiers = new FakeDossierStore();
        dossiers.Areas[Workshop] = new AreaDossier
        {
            AreaNodeId = Workshop,
            Title = "Bell-Founder's Workshop (Level 1, Area 4)",
            Creatures = new[] { new AreaCreature { Ref = "grask" } },
        };
        dossiers.Areas[Den] = new AreaDossier
        {
            AreaNodeId = Den,
            Title = "Goblin Den (Level 1, Area 2)",
            ReadAloud = "[[grask|Grask]] was here once.",
        };
        dossiers.LevelsByArea[Workshop] = level;
        dossiers.LevelsByArea[Den] = level;
        dossiers.LevelNumbers[Level1] = 1;
        dossiers.Npcs = new NpcRoster { Npcs = new[] { Npc("wenna-brask", "Wenna Brask") } };
        return dossiers;
    }

    private static NpcDossier Npc(string id, string name, params CastMember[] contacts) =>
        new NpcDossier { Id = id, Name = name, Contacts = contacts };
}
