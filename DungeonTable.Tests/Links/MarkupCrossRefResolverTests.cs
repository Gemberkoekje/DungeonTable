using System.Collections.Generic;
using DungeonTable.Core.Dossier;
using DungeonTable.Core.Stats;
using DungeonTable.Infrastructure.Links;
using DungeonTable.Tests.Web;

namespace DungeonTable.Tests.Links;

/// <summary>
/// The resolver the app serves: the links authors wrote, resolved against the content, and nothing
/// else. Prose nobody has marked up stays plain text.
/// </summary>
public sealed class MarkupCrossRefResolverTests
{
    private const string Level1 = "data_dossiers_level_1";
    private const string Level3 = "data_dossiers_level_3";
    private const string Bandit = "data_statblocks_monsters_bandit";
    private const string Bugbear = "data_statblocks_monsters_bugbear";
    private const string Golem = "data_statblocks_monsters_flesh_golem";

    private static readonly string[] WennaAndBandit = { "Wenna Brask", "bandit" };

    private static readonly string[] BrokenWords = { "the captain", "nobody" };

    private static readonly string[] BugbearThenGolem = { Bugbear, Golem };

    [Fact]
    public void A_link_covers_its_markup_and_shows_its_words()
    {
        const string text = "Four [[bandit|human bandits]] play cards.";

        CrossRef reference = Assert.Single(Resolver().Resolve(text, Level1));

        Assert.Equal(Bandit, reference.TargetId);
        Assert.Equal(CrossRefKind.Monster, reference.Kind);
        Assert.Equal("human bandits", reference.Text);
        Assert.Equal("[[bandit|human bandits]]", text.Substring(reference.Start, reference.Length));
    }

    [Fact]
    public void A_bare_link_shows_what_its_target_is_called()
    {
        IReadOnlyList<CrossRef> refs = Resolver().Resolve("[[wenna-brask]] waves at the [[bandit]].", Level1);

        Assert.Equal(WennaAndBandit, refs.Select(reference => reference.Text).ToArray());
        Assert.Equal(new[] { CrossRefKind.Npc, CrossRefKind.Monster }, refs.Select(reference => reference.Kind).ToArray());
    }

    [Fact]
    public void Prose_without_markup_links_nothing()
    {
        // A mention is not a link until an author says so: "disguised as vampires" puts no vampire here.
        MarkupCrossRefResolver resolver = Resolver();

        Assert.Empty(resolver.Resolve("A bugbear waits beside a flesh golem.", Level1));
        Assert.Empty(resolver.Resolve("A bugbear waits beside a flesh golem."));
        Assert.Empty(resolver.Resolve(string.Empty, Level1));
        Assert.Empty(resolver.Resolve(null, Level1));
    }

    [Fact]
    public void Only_the_linked_names_are_links_and_they_come_back_in_order()
    {
        const string text = "A [[bugbear]] eyes the bandit, then [[flesh-golem|the golem]].";

        IReadOnlyList<CrossRef> refs = Resolver().Resolve(text, Level1);

        Assert.Equal(BugbearThenGolem, refs.Select(reference => reference.TargetId).ToArray());
        Assert.True(refs[0].Start < refs[1].Start);
    }

    [Fact]
    public void Brackets_that_make_no_link_link_nothing()
    {
        Assert.Empty(Resolver().Resolve("A stray [[ sits beside a bugbear.", Level1));
    }

    [Fact]
    public void A_broken_link_shows_its_words_and_goes_nowhere()
    {
        IReadOnlyList<CrossRef> refs = Resolver().Resolve("[[bandit-captin|the captain]] and [[nobody]].", Level1);

        Assert.All(refs, reference => Assert.Equal(CrossRefKind.Unresolved, reference.Kind));
        Assert.All(refs, reference => Assert.Equal(string.Empty, reference.TargetId));
        Assert.Equal(BrokenWords, refs.Select(reference => reference.Text).ToArray());
    }

    [Fact]
    public void An_area_link_is_read_on_the_callers_floor_and_cached_per_floor()
    {
        MarkupCrossRefResolver resolver = Resolver();
        const string text = "A door leads to [[area 2a]].";

        CrossRef onLevel1 = Assert.Single(resolver.Resolve(text, Level1));
        CrossRef nowhere = Assert.Single(resolver.Resolve(text));

        Assert.Equal("data_dossiers_level_1_area_2a", onLevel1.TargetId);
        Assert.Equal(CrossRefKind.Unresolved, nowhere.Kind);
        Assert.Equal("area 2a", nowhere.Text);
    }

    [Fact]
    public void The_same_printed_key_names_a_different_room_on_each_floor()
    {
        // Every floor of the dungeon keys an "area 19". Asked of one resolver, so a cache keyed on the
        // text alone would hand the second floor the first floor's room.
        MarkupCrossRefResolver resolver = Resolver();
        const string text = "The bandits flee to [[area 19]].";

        Assert.Equal("data_dossiers_level_1_area_19", Assert.Single(resolver.Resolve(text, Level1)).TargetId);
        Assert.Equal("data_dossiers_level_3_area_19", Assert.Single(resolver.Resolve(text, Level3)).TargetId);
    }

    [Fact]
    public void Prose_on_no_floor_keeps_its_entity_links_and_the_area_links_that_name_their_floor()
    {
        // Quest prose and the appendix cards range over the whole dungeon: a bare "area 19" on them is
        // never about the floor in front of the DM, while one that names its floor is a jump.
        IReadOnlyList<CrossRef> refs = Resolver().Resolve("A [[bugbear]] guards [[area 19]], or is it [[L3 area 19]]?");

        Assert.Equal(Bugbear, refs[0].TargetId);
        Assert.Equal(CrossRefKind.Unresolved, refs[1].Kind);
        Assert.Equal("data_dossiers_level_3_area_19", refs[2].TargetId);
        Assert.Equal(CrossRefKind.Area, refs[2].Kind);
    }

    [Fact]
    public void An_unauthored_floor_reads_a_bare_area_link_as_broken()
    {
        // A floor with a map but no dossiers yet has no areas of its own, and must not borrow another
        // floor's.
        CrossRef reference = Assert.Single(Resolver().Resolve("They retreat through [[area 2a]].", "data_dossiers_level_9"));

        Assert.Equal(CrossRefKind.Unresolved, reference.Kind);
    }

    private static MarkupCrossRefResolver Resolver()
    {
        var stats = new FakeStatLibrary();
        stats.Monsters[Bandit] = new StatBlock { NodeId = Bandit, Name = "Bandit" };
        stats.Monsters[Bugbear] = new StatBlock { NodeId = Bugbear, Name = "Bugbear" };
        stats.Monsters[Golem] = new StatBlock { NodeId = Golem, Name = "Flesh Golem" };

        var dossiers = new FakeDossierStore();
        var level1 = new LevelDossier { LevelNodeId = Level1 };
        var level3 = new LevelDossier { LevelNodeId = Level3 };
        AddArea(dossiers, level1, "data_dossiers_level_1_area_2a", "Demon Reliefs (Level 1, Area 2a)", 2);
        AddArea(dossiers, level1, "data_dossiers_level_1_area_19", "Bell Loft (Level 1, Area 19)", 19);
        AddArea(dossiers, level3, "data_dossiers_level_3_area_19", "The Founders' Ossuary (Level 3, Area 19)", 19);
        dossiers.LevelNumbers[Level1] = 1;
        dossiers.LevelNumbers[Level3] = 3;
        dossiers.Npcs = new NpcRoster { Npcs = new[] { new NpcDossier { Id = "wenna-brask", Name = "Wenna Brask" } } };

        return new MarkupCrossRefResolver(LinkTargets.Build(dossiers, stats));
    }

    private static void AddArea(FakeDossierStore dossiers, LevelDossier level, string id, string title, int number)
    {
        dossiers.Areas[id] = new AreaDossier { AreaNodeId = id, Title = title, AreaNumber = number };
        dossiers.LevelsByArea[id] = level;
    }
}
