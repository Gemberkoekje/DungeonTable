using DungeonTable.Core.Dossier;
using DungeonTable.Core.Stats;
using DungeonTable.Infrastructure.Links;
using DungeonTable.Tests.Web;
using Qowaiv.Validation.Abstractions;

namespace DungeonTable.Tests.Links;

/// <summary>
/// What an authored link's target names: areas by printed key on a floor, people by roster id, stat
/// blocks and spells by the slug of their name, and never a guess where a name means two things.
/// </summary>
public sealed class LinkTargetsTests
{
    private const string Level1 = "data_dossiers_level_1";
    private const string Level2 = "data_dossiers_level_2";

    private static readonly LevelDossier Level1Dossier = new LevelDossier
    {
        LevelNodeId = Level1,
        Title = "Level 1: The Undercroft",
        Entities = new[]
        {
            new Entity { Id = "bell-warden", Kind = EntityKind.Creature, Name = "the Bell-Warden", StatBlock = "gargoyle" },
            new Entity { Id = "nib", Kind = EntityKind.Npc, Name = "Nib", StatBlock = "goblin" },
        },
        Factions = new[] { new DossierBlock { Id = "wickfoot-goblins", Heading = "Wickfoot goblins", Body = "A band of goblins." } },
    };

    private static readonly LevelDossier Level2Dossier = new LevelDossier { LevelNodeId = Level2, Title = "Level 2: The Sealed Crypt" };

    private static readonly NpcDossier Wenna = new NpcDossier { Id = "wenna-brask", Name = "Wenna Brask" };

    [Fact]
    public void An_area_key_is_read_on_the_floor_the_prose_belongs_to()
    {
        LinkTarget target = Resolved(Targets().Resolve("area 2a", Level1));

        Assert.Equal("data_dossiers_level_1_area_2a", target.TargetId);
        Assert.Equal(CrossRefKind.Area, target.Kind);
        Assert.Equal("area 2a", target.DisplayName);
    }

    [Fact]
    public void A_bare_area_key_names_nothing_in_prose_that_belongs_to_no_floor()
    {
        Result<LinkTarget> result = Targets().Resolve("area 2a", string.Empty);

        Assert.False(result.IsValid);
        Assert.Contains("[[L1 area 2a]]", Message(result), StringComparison.Ordinal);
    }

    [Fact]
    public void A_named_floor_reaches_another_level_from_anywhere()
    {
        LinkTargets targets = Targets();

        Assert.Equal("data_dossiers_level_2_area_14", Resolved(targets.Resolve("L2 area 14", Level1)).TargetId);
        Assert.Equal("data_dossiers_level_2_area_14", Resolved(targets.Resolve("L2 area 14", string.Empty)).TargetId);
        Assert.Equal("data_dossiers_level_1_area_2a", Resolved(targets.Resolve("L1 area 2a", Level2)).TargetId);
    }

    [Theory]
    [InlineData("Area 2A")]
    [InlineData("area  2a")]
    [InlineData("area-2a")]
    [InlineData("L1 AREA 2a")]
    public void An_area_target_ignores_case_and_spacing(string written)
    {
        Assert.Equal("data_dossiers_level_1_area_2a", Resolved(Targets().Resolve(written, Level1)).TargetId);
    }

    [Fact]
    public void An_area_title_without_a_printed_key_is_reached_by_its_number()
    {
        Assert.Equal("data_dossiers_level_1_area_7", Resolved(Targets().Resolve("area 7", Level1)).TargetId);
    }

    [Theory]
    [InlineData("area 99", Level1)]
    [InlineData("area 14", Level1)]
    [InlineData("L2 area 99", Level1)]
    [InlineData("L0 area 1", Level1)]
    [InlineData("area 1", "data_dossiers_level_5")]
    public void An_area_that_is_not_authored_names_nothing(string written, string floor)
    {
        Assert.False(Targets().Resolve(written, floor).IsValid);
    }

    [Theory]
    [InlineData("L9 area 1", Level1)]
    [InlineData("L10 area 26d", "")]
    public void An_area_on_a_floor_with_no_level_file_is_pending_rather_than_broken(string written, string floor)
    {
        // Level 1's stairs lead to Level 2 whether or not anyone has authored it yet. The link is
        // early, not wrong: nothing can check it, and it becomes a jump by itself once that floor loads.
        LinkTarget target = Resolved(Targets().Resolve(written, floor));

        Assert.Equal(CrossRefKind.Pending, target.Kind);
        Assert.Equal(string.Empty, target.TargetId);
        Assert.Equal(written, target.DisplayName);
    }

    [Fact]
    public void An_entity_a_level_declares_is_named_by_its_id_and_shown_by_name()
    {
        LinkTargets targets = Targets();

        LinkTarget warden = Resolved(targets.Resolve("bell-warden", string.Empty));
        Assert.Equal("bell-warden", warden.TargetId);
        Assert.Equal(CrossRefKind.Monster, warden.Kind);

        // A named individual is a proper name, not a kind of thing: never lower-cased like a stat block.
        Assert.Equal("the Bell-Warden", warden.DisplayName);

        Assert.Equal(CrossRefKind.Npc, Resolved(targets.Resolve("nib", Level1)).Kind);
    }

    [Fact]
    public void A_faction_block_with_an_id_is_a_faction_entity()
    {
        LinkTarget goblins = Resolved(Targets().Resolve("wickfoot-goblins", Level1));

        Assert.Equal("wickfoot-goblins", goblins.TargetId);
        Assert.Equal(CrossRefKind.Faction, goblins.Kind);
        Assert.Equal("Wickfoot goblins", goblins.DisplayName);
    }

    [Fact]
    public void An_entity_that_shares_a_stat_blocks_slug_collides_with_it()
    {
        FakeDossierStore dossiers = Dossiers();
        dossiers.Levels.Add(new LevelDossier
        {
            LevelNodeId = "data_dossiers_level_3",
            Entities = new[] { new Entity { Id = "flesh-golem", Kind = EntityKind.Creature, Name = "A golem" } },
        });
        LinkTargets targets = LinkTargets.Build(dossiers, Stats());

        Assert.False(targets.Resolve("flesh-golem", Level1).IsValid);
        Assert.Contains(targets.Collisions(), collision => collision.StartsWith("[[flesh-golem]]", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(EntityKind.Creature, CrossRefKind.Monster)]
    [InlineData(EntityKind.Npc, CrossRefKind.Npc)]
    [InlineData(EntityKind.Faction, CrossRefKind.Faction)]
    [InlineData(EntityKind.Item, CrossRefKind.Item)]
    [InlineData(EntityKind.None, CrossRefKind.None)]
    public void An_entity_is_coloured_and_opened_as_the_thing_it_is(EntityKind kind, CrossRefKind expected)
    {
        Assert.Equal(expected, LinkTargets.KindOf(kind));
    }

    [Theory]
    [InlineData("bandit-captain", true)]
    [InlineData("bell-warden", true)]
    [InlineData("nib", true)]
    [InlineData("wenna-brask", true)]
    [InlineData("maren", true)]
    [InlineData("wickfoot-goblins", false)]
    [InlineData("detect-magic", false)]
    [InlineData("area 2a", false)]
    [InlineData("L9 area 1", false)]
    public void Only_a_creature_a_person_or_a_character_can_stand_in_a_room(string written, bool creature)
    {
        Assert.Equal(creature, Resolved(Targets().Resolve(written, Level1)).CanBeInARoom);
    }

    [Theory]
    [InlineData("flesh-golem")]
    [InlineData("flesh golem")]
    [InlineData("Flesh Golem")]
    public void A_stat_block_is_named_by_the_slug_of_its_name(string written)
    {
        LinkTarget target = Resolved(Targets().Resolve(written, Level1));

        Assert.Equal("data_statblocks_monsters_flesh_golem", target.TargetId);
        Assert.Equal(CrossRefKind.Monster, target.Kind);
    }

    [Fact]
    public void A_stat_block_shows_in_lower_case_unless_its_target_is_capitalised()
    {
        LinkTargets targets = Targets();

        Assert.Equal("flesh golem", Resolved(targets.Resolve("flesh-golem", Level1)).DisplayName);
        Assert.Equal("Flesh Golem", Resolved(targets.Resolve("Flesh-Golem", Level1)).DisplayName);
    }

    [Fact]
    public void A_spell_is_named_the_way_a_stat_block_is()
    {
        LinkTarget target = Resolved(Targets().Resolve("detect-magic", string.Empty));

        Assert.Equal("data_statblocks_spells_detect_magic", target.TargetId);
        Assert.Equal(CrossRefKind.Spell, target.Kind);
        Assert.Equal("detect magic", target.DisplayName);
    }

    [Fact]
    public void People_are_named_by_their_roster_id_and_shown_by_name()
    {
        LinkTargets targets = Targets();

        LinkTarget wenna = Resolved(targets.Resolve("wenna-brask", string.Empty));
        Assert.Equal("wenna-brask", wenna.TargetId);
        Assert.Equal(CrossRefKind.Npc, wenna.Kind);
        Assert.Equal("Wenna Brask", wenna.DisplayName);

        LinkTarget maren = Resolved(targets.Resolve("maren", string.Empty));
        Assert.Equal(CrossRefKind.Character, maren.Kind);
        Assert.Equal("Maren", maren.DisplayName);
    }

    [Theory]
    [InlineData("bandit-captin")]
    [InlineData("nobody-at-all")]
    [InlineData("")]
    [InlineData("   ")]
    public void A_target_that_matches_nothing_names_nothing(string written)
    {
        Assert.False(Targets().Resolve(written, Level1).IsValid);
    }

    [Fact]
    public void A_name_two_things_share_resolves_to_neither_and_is_reported()
    {
        // An NPC whose id is also a stat block's slug: guessing either would be a silent wrong link.
        FakeDossierStore dossiers = Dossiers();
        dossiers.Npcs = new NpcRoster
        {
            Npcs = new[] { Wenna, new NpcDossier { Id = "flesh-golem", Name = "The chapel's golem" } },
        };
        LinkTargets targets = LinkTargets.Build(dossiers, Stats());

        Result<LinkTarget> result = targets.Resolve("flesh-golem", Level1);

        Assert.False(result.IsValid);
        Assert.Contains("ambiguous", Message(result), StringComparison.Ordinal);
        Assert.Contains(targets.Collisions(), collision => collision.StartsWith("[[flesh-golem]]", StringComparison.Ordinal));
    }

    [Fact]
    public void Two_areas_printed_with_one_key_on_a_floor_collide()
    {
        FakeDossierStore dossiers = Dossiers();
        AddArea(dossiers, Level1Dossier, "data_dossiers_level_1_area_5", "Guard Post (Level 1, Area 5)", 5);
        AddArea(dossiers, Level1Dossier, "data_dossiers_level_1_area_5_again", "Empty Room (Level 1, Area 5)", 5);
        LinkTargets targets = LinkTargets.Build(dossiers, Stats());

        Assert.False(targets.Resolve("area 5", Level1).IsValid);
        Assert.Contains(targets.Collisions(), collision => collision.StartsWith("'area 5'", StringComparison.Ordinal));
    }

    [Fact]
    public void Two_level_files_carrying_one_number_collide()
    {
        FakeDossierStore dossiers = Dossiers();
        dossiers.LevelNumbers[Level2] = 1;
        LinkTargets targets = LinkTargets.Build(dossiers, Stats());

        Assert.False(targets.Resolve("L1 area 2a", string.Empty).IsValid);
        Assert.Contains(targets.Collisions(), collision => collision.StartsWith("'L1'", StringComparison.Ordinal));
    }

    [Fact]
    public void The_fixture_itself_has_no_collisions()
    {
        Assert.Empty(Targets().Collisions());
    }

    [Theory]
    [InlineData("Vestry (Level 1, Area 3B)", "3b")]
    [InlineData("Undercroft Landing (Level 1, Area 1)", "1")]
    [InlineData("Untagged Closet", "")]
    [InlineData(null, "")]
    public void An_areas_printed_key_is_read_from_its_title(string title, string key)
    {
        Assert.Equal(key, LinkTargets.PrintedKeyOf(title));
    }

    [Fact]
    public void A_stat_block_or_spell_is_a_rulebook_entry_and_nothing_else_is()
    {
        LinkTargets targets = Targets();

        Assert.True(Resolved(targets.Resolve("bandit-captain", Level1)).Rulebook);
        Assert.True(Resolved(targets.Resolve("detect-magic", Level1)).Rulebook);
        Assert.False(Resolved(targets.Resolve("bell-warden", Level1)).Rulebook);
        Assert.False(Resolved(targets.Resolve("wenna-brask", Level1)).Rulebook);
        Assert.False(Resolved(targets.Resolve("area 2a", Level1)).Rulebook);
    }

    [Theory]
    [InlineData("Grask's Crew", "grasks-crew")]
    [InlineData("Will-o'-Wisp", "will-o-wisp")]
    [InlineData("  Bandit   Captain ", "bandit-captain")]
    [InlineData("wenna-brask", "wenna-brask")]
    public void A_slug_is_lower_case_drops_apostrophes_and_hyphenates_the_rest(string name, string slug)
    {
        Assert.Equal(slug, LinkTargets.Slug(name));
    }

    [Fact]
    public void A_book_entry_is_named_by_its_id_and_shown_by_name()
    {
        LinkTarget mill = Resolved(TargetsWith(Book("almanac", Entry("the-old-mill", "The Old Mill"))).Resolve("the-old-mill", string.Empty));

        Assert.Equal("the-old-mill", mill.TargetId);
        Assert.Equal(CrossRefKind.Book, mill.Kind);
        Assert.Equal("The Old Mill", mill.DisplayName);
        Assert.False(mill.Rulebook);
        Assert.False(mill.CanBeInARoom);
    }

    [Theory]
    [InlineData("wenna-brask", CrossRefKind.Npc)]
    [InlineData("bell-warden", CrossRefKind.Monster)]
    [InlineData("flesh-golem", CrossRefKind.Monster)]
    [InlineData("maren", CrossRefKind.Character)]
    public void Anything_authored_wins_over_a_book_entry_with_its_id(string id, CrossRefKind authored)
    {
        // Two books with it are no collision either: neither is a target of its own.
        LinkTargets targets = TargetsWith(Book("almanac", Entry(id, "The book's " + id)), Book("mm", Entry(id, "Another book's " + id)));

        LinkTarget found = Resolved(targets.Resolve(id, Level1));

        Assert.Equal(authored, found.Kind);
        Assert.Empty(targets.Collisions());
    }

    [Fact]
    public void Two_books_with_one_id_collide_and_resolve_to_neither()
    {
        LinkTargets targets = TargetsWith(
            Book("almanac", Entry("wish", "Wish")),
            Book("phb", Entry("wish", "Wish")));

        Assert.False(targets.Resolve("wish", string.Empty).IsValid);
        Assert.Contains(targets.Collisions(), collision => collision.Contains("[[wish]]", StringComparison.Ordinal));
    }

    private static LinkTargets TargetsWith(params BookIndex[] books) => LinkTargets.Build(Dossiers(), Stats(), books);

    private static BookIndex Book(string book, params BookEntry[] entries) =>
        new BookIndex { Book = book, Title = book, Entries = entries };

    private static BookEntry Entry(string id, string name) => new BookEntry { Id = id, Kind = "lore", Name = name };

    private static LinkTargets Targets() => LinkTargets.Build(Dossiers(), Stats());

    private static FakeDossierStore Dossiers()
    {
        var dossiers = new FakeDossierStore();
        AddArea(dossiers, Level1Dossier, "data_dossiers_level_1_area_1", "Undercroft Landing (Level 1, Area 1)", 1);
        AddArea(dossiers, Level1Dossier, "data_dossiers_level_1_area_2a", "Goblin Den (Level 1, Area 2a)", 2);
        AddArea(dossiers, Level1Dossier, "data_dossiers_level_1_area_7", "Untagged Closet", 7);
        AddArea(dossiers, Level2Dossier, "data_dossiers_level_2_area_14", "Crypt Stair (Level 2, Area 14)", 14);
        dossiers.LevelNumbers[Level1] = 1;
        dossiers.LevelNumbers[Level2] = 2;
        dossiers.Npcs = new NpcRoster { Npcs = new[] { Wenna } };
        dossiers.Party = new PartyDossier { Characters = new[] { new CharacterDossier { Id = "maren", Name = "Maren" } } };
        return dossiers;
    }

    private static void AddArea(FakeDossierStore dossiers, LevelDossier level, string nodeId, string title, int number)
    {
        dossiers.Areas[nodeId] = new AreaDossier { AreaNodeId = nodeId, Title = title, AreaNumber = number };
        dossiers.LevelsByArea[nodeId] = level;
    }

    private static FakeStatLibrary Stats()
    {
        var stats = new FakeStatLibrary();
        stats.Monsters["data_statblocks_monsters_flesh_golem"] = new StatBlock { NodeId = "data_statblocks_monsters_flesh_golem", Name = "Flesh Golem" };
        stats.Monsters["data_statblocks_monsters_bandit_captain"] = new StatBlock { NodeId = "data_statblocks_monsters_bandit_captain", Name = "Bandit Captain" };
        stats.Spells["data_statblocks_spells_detect_magic"] = new SpellEntry { NodeId = "data_statblocks_spells_detect_magic", Name = "Detect Magic" };
        return stats;
    }

    private static LinkTarget Resolved(Result<LinkTarget> result)
    {
        Assert.True(result.IsValid, Message(result));
        return result.Value;
    }

    private static string Message(Result<LinkTarget> result) =>
        string.Join(" ", result.Messages.Select(message => message.Message));
}
