using DungeonTable.Core.Art;
using DungeonTable.Core.Dossier;
using DungeonTable.Core.Stats;
using DungeonTable.Infrastructure.Links;
using DungeonTable.Tests.Web;

namespace DungeonTable.Tests.Links;

/// <summary>
/// Which pictures show what: a picture's <c>shows</c> read the way a link is, its older <c>nodeIds</c>
/// as they are, and, where several show one thing, the one most about it first; and a creature a level
/// declares that no picture shows has its stat block's, where a person has none.
/// </summary>
public sealed class PictureIndexTests
{
    private const string Level1 = "data_dossiers_level_1";
    private const string Den = "data_dossiers_level_1_area_2";
    private const string Gargoyle = "data_statblocks_monsters_gargoyle";
    private const string Goblin = "data_statblocks_monsters_goblin";

    private static readonly string[] WennaFirst = { "wenna-portrait", "millbrook-folk" };
    private static readonly string[] PortraitBeforeSheet = { "wenna-portrait", "village-sheet" };
    private static readonly string[] ByTitle = { "a-gargoyle", "b-gargoyle" };
    private static readonly string[] NibThenGoblin = { "nib", Goblin };
    private static readonly string[] OnlyNib = { "nib" };
    private static readonly string[] PortraitBeforeScene = { "goblin-portrait", "ambush" };

    [Fact]
    public void A_picture_is_found_by_what_it_shows_named_the_way_a_link_names_it()
    {
        PictureIndex index = PictureIndex.Build(
            new[]
            {
                Picture("wenna", "wenna-brask"),
                Picture("warden", "bell-warden", "Gargoyle"),
                Picture("den", "L1 area 2"),
            },
            Targets(),
            Dossiers());

        Assert.Equal("wenna", Assert.Single(index.PicturesOf("wenna-brask")).Id);
        Assert.Equal("warden", Assert.Single(index.PicturesOf("bell-warden")).Id);
        Assert.Equal("warden", Assert.Single(index.PicturesOf(Gargoyle)).Id);
        Assert.Equal("den", Assert.Single(index.PicturesOf(Den)).Id);
        Assert.Empty(index.PicturesOf(Goblin));
        Assert.Empty(index.PicturesOf(string.Empty));
    }

    [Fact]
    public void A_stat_block_named_the_older_way_by_node_id_counts_after_what_it_shows()
    {
        var older = new ArtImage { Id = "goblin", Title = "Goblin", Shows = new[] { "nib" }, NodeIds = new[] { Goblin } };

        Assert.Equal(NibThenGoblin, PictureIndex.TargetsOf(older, Targets()));
        Assert.Equal("goblin", Assert.Single(PictureIndex.Build(new[] { older }, Targets(), Dossiers()).PicturesOf(Goblin)).Id);
    }

    [Fact]
    public void A_name_that_resolves_to_nothing_or_to_a_floor_not_written_yet_shows_nothing()
    {
        var picture = Picture("lost", "wena-brask", "L2 area 1", "area 2", "nib", "Nib");

        Assert.Equal(OnlyNib, PictureIndex.TargetsOf(picture, Targets()));
    }

    [Fact]
    public void The_picture_that_names_a_thing_first_is_its_best_picture()
    {
        PictureIndex index = PictureIndex.Build(
            new[]
            {
                Picture("millbrook-folk", "nib", "wenna-brask"),
                Picture("wenna-portrait", "wenna-brask", "nib"),
            },
            Targets(),
            Dossiers());

        Assert.Equal(WennaFirst, index.PicturesOf("wenna-brask").Select(image => image.Id));
    }

    [Fact]
    public void Of_two_that_name_it_as_early_the_one_that_shows_fewer_things_comes_first()
    {
        PictureIndex index = PictureIndex.Build(
            new[]
            {
                Picture("village-sheet", "wenna-brask", "nib", "bell-warden"),
                Picture("wenna-portrait", "wenna-brask"),
            },
            Targets(),
            Dossiers());

        Assert.Equal(PortraitBeforeSheet, index.PicturesOf("wenna-brask").Select(image => image.Id));
    }

    [Fact]
    public void Otherwise_the_catalogue_order_breaks_the_tie()
    {
        PictureIndex index = PictureIndex.Build(
            new[] { Picture("a-gargoyle", "gargoyle"), Picture("b-gargoyle", "gargoyle") },
            Targets(),
            Dossiers());

        Assert.Equal(ByTitle, index.PicturesOf(Gargoyle).Select(image => image.Id));
    }

    [Fact]
    public void A_picture_of_the_thing_comes_before_a_scene_or_handout_it_is_in()
    {
        PictureIndex index = PictureIndex.Build(
            new[]
            {
                Picture("ambush", ArtKind.Handout, "goblin"),
                Picture("goblin-portrait", ArtKind.Creature, "nib", "goblin"),
            },
            Targets(),
            Dossiers());

        Assert.Equal(PortraitBeforeScene, index.PicturesOf(Goblin).Select(image => image.Id));
    }

    [Fact]
    public void A_creature_a_level_declares_that_no_picture_shows_has_its_stat_blocks_and_a_person_has_none()
    {
        // The Bell-Warden is a creature that uses the gargoyle's block; Nib is a person who uses the goblin's.
        PictureIndex index = PictureIndex.Build(
            new[] { Picture("gargoyle", "gargoyle"), Picture("goblin", "goblin") },
            Targets(),
            Dossiers());

        Assert.Equal("gargoyle", Assert.Single(index.PicturesOf("bell-warden")).Id);
        Assert.Empty(index.PicturesOf("nib"));
    }

    [Fact]
    public void A_creature_with_a_picture_of_its_own_keeps_it()
    {
        PictureIndex index = PictureIndex.Build(
            new[] { Picture("gargoyle", "gargoyle"), Picture("warden", "bell-warden") },
            Targets(),
            Dossiers());

        Assert.Equal("warden", Assert.Single(index.PicturesOf("bell-warden")).Id);
    }

    [Fact]
    public void A_name_the_book_answers_today_moves_to_the_campaigns_own_stat_block_the_day_one_is_written()
    {
        var bestiary = new BookIndex
        {
            Book = "bestiary",
            Title = "Bestiary",
            Entries = new[] { new BookEntry { Id = "ankheg", Kind = "monster", Name = "Ankheg" } },
        };
        ArtImage[] pictures = { Picture("ankheg", "ankheg") };

        PictureIndex before = PictureIndex.Build(pictures, LinkTargets.Build(Dossiers(), Stats(), new[] { bestiary }), Dossiers());
        FakeStatLibrary written = Stats();
        written.Monsters["data_statblocks_monsters_ankheg"] = new StatBlock { NodeId = "data_statblocks_monsters_ankheg", Name = "Ankheg" };
        PictureIndex after = PictureIndex.Build(pictures, LinkTargets.Build(Dossiers(), written, new[] { bestiary }), Dossiers());

        Assert.Single(before.PicturesOf("ankheg"));
        Assert.Empty(after.PicturesOf("ankheg"));
        Assert.Single(after.PicturesOf("data_statblocks_monsters_ankheg"));
    }

    [Fact]
    public void A_picture_that_names_one_thing_twice_is_listed_for_it_once()
    {
        PictureIndex index = PictureIndex.Build(new[] { Picture("warden", "Gargoyle", "gargoyle") }, Targets(), Dossiers());

        Assert.Single(index.PicturesOf(Gargoyle));
    }

    private static ArtImage Picture(string id, params string[] shows) => new ArtImage { Id = id, Title = id, Shows = shows };

    private static ArtImage Picture(string id, ArtKind kind, params string[] shows) => new ArtImage { Id = id, Title = id, Kind = kind, Shows = shows };

    private static LinkTargets Targets() => LinkTargets.Build(Dossiers(), Stats());

    private static FakeDossierStore Dossiers()
    {
        var level = new LevelDossier
        {
            LevelNodeId = Level1,
            Title = "Level 1: The Undercroft",
            Entities = new[]
            {
                new Entity { Id = "bell-warden", Kind = EntityKind.Creature, Name = "the Bell-Warden", StatBlock = "gargoyle" },
                new Entity { Id = "nib", Kind = EntityKind.Npc, Name = "Nib", StatBlock = "goblin" },
            },
        };
        var dossiers = new FakeDossierStore();
        dossiers.Areas[Den] = new AreaDossier { AreaNodeId = Den, Title = "Goblin Den (Level 1, Area 2)", AreaNumber = 2 };
        dossiers.LevelsByArea[Den] = level;
        dossiers.LevelNumbers[Level1] = 1;
        dossiers.Npcs = new NpcRoster { Npcs = new[] { new NpcDossier { Id = "wenna-brask", Name = "Wenna Brask" } } };
        return dossiers;
    }

    private static FakeStatLibrary Stats()
    {
        var stats = new FakeStatLibrary();
        stats.Monsters[Gargoyle] = new StatBlock { NodeId = Gargoyle, Name = "Gargoyle" };
        stats.Monsters[Goblin] = new StatBlock { NodeId = Goblin, Name = "Goblin" };
        return stats;
    }
}
