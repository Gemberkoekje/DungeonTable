using System.Collections.Generic;
using DungeonTable.Core.Briefing;
using DungeonTable.Core.Dossier;
using DungeonTable.Core.Stats;
using DungeonTable.Infrastructure.Links;
using DungeonTable.Tests.Web;
using Qowaiv.Validation.Abstractions;

namespace DungeonTable.Tests.Links;

/// <summary>
/// Briefings, cards and search from the authored content, with the book index below it: an area's
/// briefing is its own creature and exit lists, an entity a level declares gets its card from what
/// the level says about it, a stat block's card comes from the library, and search runs from the
/// entities through the campaign's own content to the book.
/// </summary>
public sealed class AuthoredProjectionTests
{
    private const string Level1 = "data_dossiers_level_1";
    private const string Landing = "data_dossiers_level_1_area_1";
    private const string Den = "data_dossiers_level_1_area_2";
    private const string Workshop = "data_dossiers_level_1_area_4";
    private const string Bandit = "data_statblocks_monsters_bandit";
    private const string BanditCaptain = "data_statblocks_monsters_bandit_captain";
    private const string FleshGolem = "data_statblocks_monsters_flesh_golem";
    private const string Acolyte = "data_statblocks_monsters_acolyte";
    private const string DetectMagic = "data_statblocks_spells_detect_magic";

    // An entity match, then the stat block of the same name (CA1861).
    private static readonly string[] EntityThenBlock = { "foundry-golem", FleshGolem };

    // An exact name, then a prefix (CA1861).
    private static readonly string[] ExactThenPrefix = { Bandit, BanditCaptain };

    // Nib's relations as they read from his side, in the order they were written.
    private static readonly string[] NibsRelations = { "member of", "hunted by" };

    // The campaign's own area, then the book's entry.
    private static readonly string[] AreaThenBook = { Landing, "the-old-mill" };

    // The level's entity, then the book's entry.
    private static readonly string[] EntityThenBook = { "foundry-golem", "golems" };

    // An entry named for the words, then one whose description only mentions them.
    private static readonly string[] NameThenDescription = { "millbrook", "the-old-mill" };

    [Fact]
    public void An_area_that_lists_nothing_has_nobody_there_and_no_exits()
    {
        Fixture fixture = new Fixture();
        fixture.Area(Den, "Goblin Den (Level 1, Area 2)");

        RoomBriefing briefing = Valid(fixture.Projection().GetBriefing(Den));

        Assert.Equal(Den, briefing.RoomId);
        Assert.Equal("Goblin Den (Level 1, Area 2)", briefing.Label);
        Assert.Empty(briefing.Occupants);
        Assert.Empty(briefing.Connections);
        Assert.Empty(briefing.Involvements);
    }

    [Fact]
    public void An_id_no_area_has_opens_no_briefing()
    {
        AuthoredProjection projection = new Fixture().Projection();

        Assert.False(projection.GetBriefing("data_dossiers_level_1_area_99").IsValid);
        Assert.False(projection.GetBriefing("   ").IsValid);
    }

    [Fact]
    public void Authored_creatures_are_the_occupants_with_their_counts_notes_and_names()
    {
        Fixture fixture = new Fixture();
        fixture.Area(Landing, "Undercroft Landing (Level 1, Area 1)", creatures: new[]
        {
            new AreaCreature { Ref = "bandit", Count = "4", Note = "around the fire" },
            new AreaCreature { Ref = "foundry-golem", Secret = true },
            new AreaCreature { Ref = "wenna-brask" },
        });

        RoomBriefing briefing = Valid(fixture.Projection().GetBriefing(Landing));

        Assert.Equal(3, briefing.Occupants.Count);

        OccupantEntry bandits = briefing.Occupants[0];
        Assert.Equal(Bandit, bandits.NodeId);
        Assert.Equal("Bandit", bandits.Label);
        Assert.Equal(Bandit, bandits.MonsterRef);
        Assert.Equal("4", bandits.Count);
        Assert.Equal("around the fire", bandits.Note);

        OccupantEntry golem = briefing.Occupants[1];
        Assert.Equal("foundry-golem", golem.NodeId);
        Assert.Equal("the foundry golem", golem.Label);
        Assert.Equal(FleshGolem, golem.MonsterRef);
        Assert.True(golem.Secret);

        OccupantEntry wenna = briefing.Occupants[2];
        Assert.Equal("wenna-brask", wenna.NodeId);
        Assert.Equal("Wenna Brask", wenna.Label);
        Assert.Equal(string.Empty, wenna.MonsterRef);
    }

    [Fact]
    public void Authored_exits_are_the_connections()
    {
        Fixture fixture = new Fixture();
        fixture.Area(Den, "Goblin Den (Level 1, Area 2)");
        fixture.Area(Landing, "Undercroft Landing (Level 1, Area 1)", exits: new[]
        {
            new AreaExit { To = "area 2", Note = "a wide arch" },
            new AreaExit { To = "L2 area 1", Note = "the stairs down" },
            new AreaExit { To = "area 99", Secret = true },
        });

        RoomBriefing briefing = Valid(fixture.Projection().GetBriefing(Landing));

        Assert.Empty(briefing.Occupants);

        ConnectionEntry arch = briefing.Connections[0];
        Assert.Equal(Den, arch.TargetRoomId);
        Assert.Equal("Goblin Den (Level 1, Area 2)", arch.Label);
        Assert.Equal("a wide arch", arch.Note);

        // Level 2 has no file yet: the exit waits for it rather than being an error.
        ConnectionEntry stairs = briefing.Connections[1];
        Assert.Equal(string.Empty, stairs.TargetRoomId);
        Assert.True(stairs.Pending);
        Assert.Equal("L2 area 1", stairs.Label);

        ConnectionEntry broken = briefing.Connections[2];
        Assert.Equal(string.Empty, broken.TargetRoomId);
        Assert.False(broken.Pending);
        Assert.True(broken.Secret);
    }

    [Theory]
    [InlineData("bandit-captin")]
    [InlineData("wickfoot-goblins")]
    [InlineData("area 2")]
    [InlineData("detect-magic")]
    public void A_creature_entry_that_names_no_creature_is_shown_as_written_with_no_id(string written)
    {
        Fixture fixture = new Fixture();
        fixture.Area(Den, "Goblin Den (Level 1, Area 2)");
        fixture.Area(Landing, "Undercroft Landing (Level 1, Area 1)", creatures: new[] { new AreaCreature { Ref = written, Count = "2" } });

        OccupantEntry occupant = Assert.Single(Valid(fixture.Projection().GetBriefing(Landing)).Occupants);

        Assert.Equal(string.Empty, occupant.NodeId);
        Assert.Equal(written, occupant.Label);
        Assert.Equal("2", occupant.Count);
    }

    [Fact]
    public void An_entitys_card_is_what_its_level_says_about_it()
    {
        Fixture fixture = new Fixture();
        fixture.Area(Workshop, "Bell-Founder's Workshop (Level 1, Area 4)", creatures: new[] { new AreaCreature { Ref = "foundry-golem" } });
        fixture.Area(Landing, "Undercroft Landing (Level 1, Area 1)", readAloud: "The [[foundry-golem|golem]] once came this far.");

        ReferenceCard card = Valid(fixture.Projection().GetReferenceCard("foundry-golem"));

        Assert.Equal("foundry-golem", card.NodeId);
        Assert.Equal("the foundry golem", card.Label);
        Assert.Equal(CrossRefKind.Monster, card.Kind);
        Assert.Equal("Wandered up from level 2; guards the anvil.", card.Summary);
        Assert.Equal(FleshGolem, card.StatBlockId);
        Assert.Equal(Level1, card.LevelNodeId);
        Assert.Equal(new[] { Workshop, Landing }, card.Areas.Select(area => area.NodeId).ToArray());
        Assert.Equal(new[] { Backlinks.InTheRoom, Backlinks.Mentioned }, card.Areas.Select(area => area.Relation).ToArray());
    }

    [Fact]
    public void A_faction_blocks_card_is_its_write_up()
    {
        ReferenceCard card = Valid(new Fixture().Projection().GetReferenceCard("wickfoot-goblins"));

        Assert.Equal("Wickfoot goblins", card.Label);
        Assert.Equal(CrossRefKind.Faction, card.Kind);
        Assert.Equal("A band of goblins.", card.Summary);
        Assert.Equal(string.Empty, card.StatBlockId);
    }

    [Theory]
    [InlineData("foundry-golem")]
    [InlineData("detect-magic")]
    [InlineData("no-such-block")]
    public void An_entity_uses_a_stat_block_or_none_never_another_entity_or_a_spell(string statBlock)
    {
        Fixture fixture = new Fixture();
        fixture.Level.Add(new Entity { Id = "shadow-golem", Kind = EntityKind.Creature, Name = "Its shadow", StatBlock = statBlock });

        ReferenceCard card = Valid(fixture.Projection().GetReferenceCard("shadow-golem"));

        Assert.Equal(string.Empty, card.StatBlockId);
    }

    [Fact]
    public void An_npc_listed_in_a_room_fights_with_the_stat_block_their_entry_names()
    {
        // The campaign's own people use a block the way a level's individuals do: without one, an
        // NPC listed in a room would join a fight with no numbers at all.
        Fixture fixture = FixtureWithAldous("acolyte");
        fixture.Area(Landing, "Undercroft Landing (Level 1, Area 1)", creatures: new[]
        {
            new AreaCreature { Ref = "brother-aldous" },
            new AreaCreature { Ref = "wenna-brask" },
        });

        RoomBriefing briefing = Valid(fixture.Projection().GetBriefing(Landing));

        OccupantEntry aldous = briefing.Occupants[0];
        Assert.Equal("brother-aldous", aldous.NodeId);
        Assert.Equal("Brother Aldous", aldous.Label);
        Assert.Equal(Acolyte, aldous.MonsterRef);
        Assert.Equal(string.Empty, briefing.Occupants[1].MonsterRef);
    }

    [Theory]
    [InlineData("foundry-golem")]
    [InlineData("wenna-brask")]
    [InlineData("detect-magic")]
    [InlineData("no-such-block")]
    public void An_npc_uses_a_stat_block_or_none_never_another_individual_or_a_spell(string statBlock)
    {
        Fixture fixture = FixtureWithAldous(statBlock);
        fixture.Area(Landing, "Undercroft Landing (Level 1, Area 1)", creatures: new[] { new AreaCreature { Ref = "brother-aldous" } });

        AuthoredProjection projection = fixture.Projection();

        Assert.Equal(string.Empty, Assert.Single(Valid(projection.GetBriefing(Landing)).Occupants).MonsterRef);
        Assert.Equal(string.Empty, projection.StatBlockOf("brother-aldous"));
    }

    [Fact]
    public void The_stat_block_an_individual_uses_is_found_by_its_id()
    {
        AuthoredProjection projection = FixtureWithAldous("acolyte").Projection();

        Assert.Equal(Acolyte, projection.StatBlockOf("brother-aldous"));
        Assert.Equal(FleshGolem, projection.StatBlockOf("foundry-golem"));
        Assert.Equal(string.Empty, projection.StatBlockOf("wenna-brask"));
        Assert.Equal(string.Empty, projection.StatBlockOf(Bandit));
        Assert.Equal(string.Empty, projection.StatBlockOf("nobody-at-all"));
        Assert.Equal(string.Empty, projection.StatBlockOf("  "));
    }

    [Fact]
    public void A_stat_blocks_card_comes_from_the_library_cited_by_its_books_title()
    {
        Fixture fixture = BookFixture();
        fixture.Books.Add(new BookIndex { Book = "srd", Title = "SRD 5.1", Source = "SRD_CC_v5.1.pdf" });
        fixture.Area(Landing, "Undercroft Landing (Level 1, Area 1)", creatures: new[] { new AreaCreature { Ref = "bandit", Count = "4" } });

        ReferenceCard card = Valid(fixture.Projection().GetReferenceCard(Bandit));

        Assert.Equal(Bandit, card.NodeId);
        Assert.Equal("Bandit", card.Label);
        Assert.Equal(CrossRefKind.Monster, card.Kind);
        Assert.Equal("SRD 5.1 p.396", card.Citation);
        Assert.Equal(string.Empty, card.StatBlockId);
        Assert.Equal(Backlinks.InTheRoom, Assert.Single(card.Areas).Relation);
    }

    [Fact]
    public void A_source_no_book_names_is_cited_as_written_without_its_pdf()
    {
        // No book index at all: the spell's source file is all there is to cite.
        ReferenceCard card = Valid(new Fixture().Projection().GetReferenceCard(DetectMagic));

        Assert.Equal("Detect Magic", card.Label);
        Assert.Equal(CrossRefKind.Spell, card.Kind);
        Assert.Equal("Grimoire p.134", card.Citation);
    }

    [Fact]
    public void An_areas_card_says_it_is_an_area_and_an_unknown_id_has_none()
    {
        Fixture fixture = new Fixture();
        fixture.Area(Den, "Goblin Den (Level 1, Area 2)");
        AuthoredProjection projection = fixture.Projection();

        ReferenceCard card = Valid(projection.GetReferenceCard(Den));

        Assert.Equal(CrossRefKind.Area, card.Kind);
        Assert.Equal("Goblin Den (Level 1, Area 2)", card.Label);
        Assert.False(projection.GetReferenceCard("nothing-at-all").IsValid);
        Assert.False(projection.GetReferenceCard(" ").IsValid);
    }

    [Fact]
    public void Entities_are_searched_first_then_the_campaigns_own_content()
    {
        AuthoredProjection projection = new Fixture().Projection();

        IReadOnlyList<SearchMatch> matches = projection.SearchNodes("golem", 10);

        Assert.Equal(EntityThenBlock, matches.Select(match => match.Id).ToArray());
        Assert.Equal("creature", matches[0].Category);
        Assert.Equal("monster", matches[1].Category);
        Assert.Single(projection.SearchNodes("golem", 1));
        Assert.Empty(projection.SearchNodes("  ", 10));
        Assert.Empty(projection.SearchNodes("golem", 0));
    }

    [Fact]
    public void The_campaigns_areas_people_stat_blocks_and_spells_are_found_by_name()
    {
        Fixture fixture = new Fixture();
        fixture.Area(Landing, "Undercroft Landing (Level 1, Area 1)");
        fixture.Dossiers.Party = new PartyDossier { Characters = new[] { new CharacterDossier { Id = "maren", Name = "Maren" } } };
        AuthoredProjection projection = fixture.Projection();

        Assert.Equal(ExactThenPrefix, projection.SearchNodes("bandit", 10).Select(match => match.Id).ToArray());
        Assert.Equal("area", Assert.Single(projection.SearchNodes("landing", 10)).Category);
        Assert.Equal("npc", Assert.Single(projection.SearchNodes("wenna", 10)).Category);
        Assert.Equal("character", Assert.Single(projection.SearchNodes("maren", 10)).Category);
        Assert.Equal(DetectMagic, Assert.Single(projection.SearchNodes("detect", 10)).Id);
    }

    [Fact]
    public void An_area_is_found_by_its_block_headings_below_every_name()
    {
        Fixture fixture = new Fixture();
        fixture.Area(Den, "Goblin Den (Level 1, Area 2)", detail: new[] { new DossierBlock { Heading = "The Loose Flagstone", Body = "It rocks." } });
        fixture.Area(Landing, "Undercroft Landing (Level 1, Area 1)", detail: new[] { new DossierBlock { Heading = "Landing Stairs", Body = "Down they go." } });
        AuthoredProjection projection = fixture.Projection();

        SearchMatch flagstone = Assert.Single(projection.SearchNodes("flagstone", 10));

        // Labelled with the area's title, which is what the Room Editor names a region after.
        Assert.Equal(Den, flagstone.Id);
        Assert.Equal("Goblin Den (Level 1, Area 2)", flagstone.Label);
        Assert.Equal("area", flagstone.Category);
        Assert.Equal("The Loose Flagstone", flagstone.Citation);

        // An area found by its title is not listed a second time for a heading.
        SearchMatch landing = Assert.Single(projection.SearchNodes("landing", 10));
        Assert.Equal(string.Empty, landing.Citation);
    }

    [Fact]
    public void A_book_entry_gets_its_card_from_the_book_index()
    {
        Fixture fixture = BookFixture();
        AuthoredProjection projection = fixture.Projection();

        ReferenceCard mill = Valid(projection.GetReferenceCard("the-old-mill"));

        Assert.Equal("The Old Mill", mill.Label);
        Assert.Equal(CrossRefKind.Book, mill.Kind);
        Assert.Equal("The watermill by the ford, in Millbrook.", mill.Summary);
        Assert.Equal("A Millbrook Almanac, p. 6", mill.Citation);
        RelationLine owner = Assert.Single(mill.Relations);
        Assert.Equal("owned and operated by", owner.Phrase);
        Assert.Equal("wenna-brask", owner.OtherId);

        // An entry with no page found is cited by its book alone.
        Assert.Equal("A Millbrook Almanac", Valid(projection.GetReferenceCard("millbrook")).Citation);
    }

    [Fact]
    public void An_authored_entry_wins_over_the_books_and_the_books_only_extends_it()
    {
        Fixture fixture = BookFixture();
        AuthoredProjection projection = fixture.Projection();

        // Nib is the level's entity; the book's line about him is kept beside his card, not instead.
        ReferenceCard nib = Valid(projection.GetReferenceCard("nib"));
        Assert.Equal(CrossRefKind.Npc, nib.Kind);

        ReferenceCard fromTheBook = Valid(projection.GetBookCard("nib"));
        Assert.Equal(CrossRefKind.Book, fromTheBook.Kind);
        Assert.Equal("Boss of the Wickfoot goblins, in the book's words.", fromTheBook.Summary);

        Assert.False(projection.GetBookCard("nothing-at-all").IsValid);
    }

    [Fact]
    public void The_book_index_is_searched_after_the_campaigns_own_content()
    {
        Fixture fixture = BookFixture();
        fixture.Area(Landing, "Old Mill Cellar (Level 1, Area 1)");
        AuthoredProjection projection = fixture.Projection();

        IReadOnlyList<SearchMatch> matches = projection.SearchNodes("old mill", 10);

        Assert.Equal(AreaThenBook, matches.Select(match => match.Id).ToArray());
        Assert.Equal("book", matches[1].Category);
        Assert.Equal("ALMANAC, p. 6", matches[1].Citation);
    }

    [Fact]
    public void An_entity_is_found_before_a_book_entry_for_the_same_words()
    {
        Fixture fixture = BookFixture();
        fixture.Stats.Monsters.Remove(FleshGolem);
        fixture.Books.Add(new BookIndex
        {
            Book = "srd",
            Title = "SRD 5.1",
            Entries = new[] { new BookEntry { Id = "golems", Kind = "creature", Name = "Golems" } },
        });
        AuthoredProjection projection = fixture.Projection();

        Assert.Equal(EntityThenBook, projection.SearchNodes("golem", 10).Select(match => match.Id).ToArray());
    }

    [Fact]
    public void A_book_entry_the_campaign_also_has_is_found_once_as_the_campaigns_own()
    {
        Fixture fixture = BookFixture();
        AuthoredProjection projection = fixture.Projection();

        // The book has entries under Wenna's and Nib's ids too; each is found once, as the campaign's.
        SearchMatch wenna = Assert.Single(projection.SearchNodes("wenna", 10));
        Assert.Equal("wenna-brask", wenna.Id);
        Assert.Equal("Wenna Brask", wenna.Label);
        Assert.Equal("npc", wenna.Category);
        Assert.Equal(string.Empty, wenna.Citation);

        SearchMatch nib = Assert.Single(projection.SearchNodes("nib", 10));
        Assert.Equal("npc", nib.Category);
    }

    [Fact]
    public void A_book_entry_a_stat_block_answers_for_opens_the_stat_block()
    {
        // The book's "bandit" entry is the Bandit stat block's slug, so it is the block, found by the
        // block's own id: a hit under the book's id would open nothing.
        Fixture fixture = BookFixture();
        fixture.Books.Add(new BookIndex
        {
            Book = "srd",
            Title = "SRD 5.1",
            Entries = new[] { new BookEntry { Id = "bandit", Kind = "creature", Name = "Highwayman", Description = "A bandit of the roads." } },
        });
        AuthoredProjection projection = fixture.Projection();

        IReadOnlyList<SearchMatch> matches = projection.SearchNodes("highwayman", 10);

        Assert.Equal(Bandit, Assert.Single(matches).Id);
        Assert.Equal("monster", matches[0].Category);
    }

    [Fact]
    public void What_the_book_says_about_an_entry_is_searched_last()
    {
        Fixture fixture = BookFixture();
        AuthoredProjection projection = fixture.Projection();

        IReadOnlyList<SearchMatch> matches = projection.SearchNodes("millbrook", 10);

        // The name first, then the entry that only mentions it.
        Assert.Equal(NameThenDescription, matches.Select(match => match.Id).ToArray());
    }

    [Fact]
    public void An_entitys_card_and_a_persons_relations_come_from_the_relation_index()
    {
        Fixture fixture = new Fixture();
        fixture.Relations.Add(new Relation { A = "nib", Rel = "member-of", B = "wickfoot-goblins" });
        fixture.Relations.Add(new Relation { A = "wenna-brask", Rel = "hunts", B = "nib", Secret = true });
        AuthoredProjection projection = fixture.Projection();

        ReferenceCard card = Valid(projection.GetReferenceCard("nib"));

        Assert.Equal(NibsRelations, card.Relations.Select(line => line.Phrase).ToArray());
        Assert.Equal("hunts", Assert.Single(projection.RelationsOf("wenna-brask")).Kind);
        Assert.Equal("has as a member", Assert.Single(Valid(projection.GetReferenceCard("wickfoot-goblins")).Relations).Phrase);
        Assert.Empty(projection.RelationsOf(Bandit));
    }

    [Fact]
    public void A_room_says_who_its_listed_individuals_are_involved_with_and_no_one_else()
    {
        Fixture fixture = new Fixture();
        fixture.Relations.Add(new Relation { A = "nib", Rel = "rival", B = "foundry-golem" });
        fixture.Area(Landing, "Undercroft Landing (Level 1, Area 1)", creatures: new[]
        {
            new AreaCreature { Ref = "bandit", Count = "4" },
            new AreaCreature { Ref = "nib" },
            new AreaCreature { Ref = "wenna-brask" },
        });

        RoomBriefing briefing = Valid(fixture.Projection().GetBriefing(Landing));

        Involvement nib = Assert.Single(briefing.Involvements);
        Assert.Equal("nib", nib.SubjectId);
        Assert.Equal("Nib", nib.SubjectLabel);
        Assert.Equal("the foundry golem", Assert.Single(nib.Lines).OtherLabel);
    }

    // A fixture whose book index has the Old Mill (run by Wenna), Millbrook itself (no page), and
    // entries under Wenna's and Nib's authored ids.
    private static Fixture BookFixture()
    {
        Fixture fixture = new Fixture();
        fixture.Books.Add(new BookIndex
        {
            Book = "almanac",
            Title = "A Millbrook Almanac",
            Entries = new[]
            {
                new BookEntry { Id = "the-old-mill", Kind = "lore", Name = "The Old Mill", Description = "The watermill by the ford, in Millbrook.", Page = 6 },
                new BookEntry { Id = "millbrook", Kind = "lore", Name = "Millbrook", Description = "A village by a ford." },
                new BookEntry { Id = "wenna-brask", Kind = "npc", Name = "Wenna Brask", Description = "The miller." },
                new BookEntry { Id = "nib", Kind = "npc", Name = "Nib", Description = "Boss of the Wickfoot goblins, in the book's words." },
            },
            Links = new[] { new Relation { A = "the-old-mill", Rel = RelationKinds.Related, B = "wenna-brask", Note = "owned and operated by" } },
        });
        return fixture;
    }

    // The fixture with the acolyte block, and Brother Aldous in the NPC roster beside Wenna, using
    // whatever stat block the test names.
    private static Fixture FixtureWithAldous(string statBlock)
    {
        Fixture fixture = new Fixture();
        fixture.Stats.Monsters[Acolyte] = new StatBlock { NodeId = Acolyte, Name = "Acolyte" };
        fixture.Dossiers.Npcs = new NpcRoster
        {
            Npcs = new[]
            {
                new NpcDossier { Id = "brother-aldous", Name = "Brother Aldous", StatBlock = statBlock },
                new NpcDossier { Id = "wenna-brask", Name = "Wenna Brask" },
            },
        };
        return fixture;
    }

    private static T Valid<T>(Result<T> result)
    {
        Assert.True(result.IsValid, string.Join(" ", result.Messages.Select(message => message.Message)));
        return result.Value;
    }

    // A Level 1 with the foundry golem, Nib and the Wickfoot goblins declared, three stat blocks, a spell
    // and one NPC; areas are added per test.
    private sealed class Fixture
    {
        public FakeDossierStore Dossiers { get; } = new FakeDossierStore();

        public FakeStatLibrary Stats { get; } = new FakeStatLibrary();

        public List<Relation> Relations { get; } = new List<Relation>();

        public List<Entity> Level { get; } = new List<Entity>
        {
            new Entity
            {
                Id = "foundry-golem",
                Kind = EntityKind.Creature,
                Name = "the foundry golem",
                StatBlock = "flesh-golem",
                Description = "Wandered up from level 2; guards the anvil.",
            },
            new Entity { Id = "nib", Kind = EntityKind.Npc, Name = "Nib", StatBlock = "bandit-captain" },
        };

        private LevelDossier level;

        public Fixture()
        {
            Stats.Monsters[Bandit] = new StatBlock { NodeId = Bandit, Name = "Bandit", Source = "SRD_CC_v5.1.pdf", SourceLocation = "p.396" };
            Stats.Monsters[BanditCaptain] = new StatBlock { NodeId = BanditCaptain, Name = "Bandit Captain" };
            Stats.Monsters[FleshGolem] = new StatBlock { NodeId = FleshGolem, Name = "Flesh Golem" };
            Stats.Spells[DetectMagic] = new SpellEntry { NodeId = DetectMagic, Name = "Detect Magic", Source = "Grimoire.pdf", SourceLocation = "p.134" };
            Dossiers.Npcs = new NpcRoster { Npcs = new[] { new NpcDossier { Id = "wenna-brask", Name = "Wenna Brask" } } };
            Dossiers.LevelNumbers[Level1] = 1;
        }

        // The book index, when a test gives it one.
        public List<BookIndex> Books { get; } = new List<BookIndex>();

        public void Area(
            string nodeId,
            string title,
            IReadOnlyList<AreaCreature> creatures = null,
            IReadOnlyList<AreaExit> exits = null,
            string readAloud = "",
            IReadOnlyList<DossierBlock> detail = null)
        {
            Dossiers.Areas[nodeId] = new AreaDossier
            {
                AreaNodeId = nodeId,
                Title = title,
                ReadAloud = readAloud,
                Creatures = creatures ?? Array.Empty<AreaCreature>(),
                Exits = exits ?? Array.Empty<AreaExit>(),
                Detail = detail ?? Array.Empty<DossierBlock>(),
            };
        }

        public AuthoredProjection Projection()
        {
            // Every area is on the one floor, whose dossier declares the entities as they stand now.
            level ??= new LevelDossier
            {
                LevelNodeId = Level1,
                Entities = Level,
                Factions = new[] { new DossierBlock { Id = "wickfoot-goblins", Heading = "Wickfoot goblins", Body = "A band of goblins." } },
                Relations = Relations,
            };

            foreach (string area in Dossiers.Areas.Keys)
            {
                Dossiers.LevelsByArea[area] = level;
            }

            if (!Dossiers.Levels.Contains(level))
            {
                Dossiers.Levels.Add(level);
            }

            return Books.Count == 0
                ? new AuthoredProjection(Dossiers, Stats, LinkTargets.Build(Dossiers, Stats))
                : new AuthoredProjection(Dossiers, Stats, LinkTargets.Build(Dossiers, Stats, Books), Books);
        }
    }
}
