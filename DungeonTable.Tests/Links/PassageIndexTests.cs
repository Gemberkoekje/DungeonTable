using System.Collections.Generic;
using System.IO;
using DungeonTable.Core.Dossier;
using DungeonTable.Infrastructure.Books;
using DungeonTable.Infrastructure.Dossiers;
using DungeonTable.Infrastructure.Links;
using DungeonTable.Infrastructure.Stats;
using DungeonTable.Tests.Web;
using Qowaiv.Validation.Abstractions;

namespace DungeonTable.Tests.Links;

/// <summary>
/// Find anything's text half: passages found by their words, on the sample pack read the way the app
/// reads it, and on content built in the test where the rank and the cut need exact control.
/// </summary>
public sealed class PassageIndexTests
{
    private const string Level1 = "data_dossiers_level_1";
    private const string Landing = "data_dossiers_level_1_area_1";
    private const string LowerNave = "data_dossiers_level_1_area_3";

    private static readonly FileSystemDossierStore Dossiers = new(Path.Combine(SamplePack.Root, "data", "dossiers"));

    private static readonly FileSystemStatLibrary Stats = new(Path.Combine(SamplePack.Root, "data", "statblocks"));

    private static readonly IReadOnlyList<BookIndex> Books =
        new FileSystemBookIndexStore(Path.Combine(SamplePack.Root, "data", "book-index")).AllBooks();

    private static readonly AuthoredProjection Projection = new(Dossiers, Stats, LinkTargets.Build(Dossiers, Stats, Books), Books);

    private static readonly string[] BeforeHeadingWordsBeforeBodyPhraseBeforeBodyWords =
    {
        "rules/sand-pit",
        "rules/pit-of-sand",
        "rules/footing",
        "rules/scattered",
    };

    private static readonly string[] RulesBeforeRoomsBeforeQuests =
    {
        "rules/the-bell",
        "area/data_dossiers_level_1_area_1/detail/the-bell",
        "entity/bell-warden",
        "quest/the-lost-hymnal/hook",
    };

    private static readonly string[] DuplicateHeadings = { "rules/doors", "rules/doors-2", "rules/darkness" };

    [Fact]
    public void A_rules_section_is_found_by_its_heading_first()
    {
        TextMatch first = Projection.FindText("darkness below", 10)[0];

        Assert.Equal("rules/darkness-below", first.Passage.Key);
        Assert.Equal(PassageKind.Rule, first.Passage.Kind);
        Assert.Equal("Darkness Below", first.Passage.Title);
        Assert.Equal("Rules of the undercroft", first.Passage.Trail);
        Assert.Equal(string.Empty, first.Passage.Floor);
    }

    [Fact]
    public void A_rooms_read_aloud_is_found_by_a_word_in_it_and_belongs_to_the_room()
    {
        TextMatch hit = Projection.FindText("tallow", 10).First(match => match.Passage.Kind == PassageKind.Area && match.Passage.Title == "Read-aloud");

        Assert.Equal($"area/{Landing}/read-aloud", hit.Passage.Key);
        Assert.Equal(PassageKind.Area, hit.Passage.Kind);
        Assert.Equal("Read-aloud", hit.Passage.Title);
        Assert.Equal("Undercroft Landing (Level 1, Area 1)", hit.Passage.Trail);
        Assert.Equal(Landing, hit.Passage.OwnerId);
        Assert.Equal(Level1, hit.Passage.Floor);
        Assert.Contains("smells of tallow and cold ashes", hit.Snippet, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_word_must_be_there_in_any_order()
    {
        Assert.Contains(Projection.FindText("tallow STAIR", 10), hit => hit.Passage.Key == $"area/{Landing}/read-aloud");
        Assert.Empty(Projection.FindText("tallow dragon", 10));
    }

    [Fact]
    public void A_link_is_matched_and_shown_by_the_words_it_shows()
    {
        // "[[millbrook-chapel|Millbrook's chapel]]" and "[[grask]]": the DM reads "Millbrook's chapel" and
        // "Grask", so that is what is found and shown, never the markup.
        TextMatch hit = Projection.FindText("millbrook's chapel", 50)
            .First(match => match.Passage.Key == PassageKeys.LevelOverview(Level1));

        Assert.Equal("What dwells here", hit.Passage.Title);
        Assert.Equal("Level 1: The Undercroft", hit.Passage.Trail);
        Assert.Contains("Below Millbrook's chapel lies", hit.Snippet, StringComparison.Ordinal);
        Assert.Contains("Grask", hit.Snippet, StringComparison.Ordinal);
        Assert.DoesNotContain("[[", hit.Snippet, StringComparison.Ordinal);
        Assert.Contains("[[grask]]", hit.Passage.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void A_floors_facts_are_filed_under_the_keys_the_pin_buttons_build()
    {
        Result<LevelDossier> level = Dossiers.GetLevel(Level1);
        Assert.True(level.IsValid);

        string faction = PassageKeys.Factions(Level1, level.Value.Factions)[0];
        string wandering = PassageKeys.WanderingMonsters(Level1, level.Value.WanderingMonsters)[1];

        Assert.Equal("level/data_dossiers_level_1/factions/grasks-crew", faction);
        Assert.Equal("Grask's crew", Projection.GetPassage(faction).Value.Title);
        Assert.Equal("1-4: Goblin Fetchers", Projection.GetPassage(wandering).Value.Title);
        Assert.Equal("Aftermath", Projection.GetPassage(PassageKeys.LevelAftermath(Level1)).Value.Title);
        Assert.Equal("Darkness Below", Projection.GetPassage(PassageKeys.Rules(Dossiers.GetReference().Value.Sections)[0]).Value.Title);
        Assert.False(Projection.GetPassage("rules/no-such-section").IsValid);
    }

    [Fact]
    public void A_heading_with_nothing_under_it_is_not_a_passage()
    {
        // The Lower Nave has no read-aloud of its own: nothing there answers anything.
        Assert.False(Projection.GetPassage($"area/{LowerNave}/read-aloud").IsValid);
        Assert.True(Projection.GetPassage($"area/{Landing}/read-aloud").IsValid);
    }

    [Fact]
    public void Nothing_is_found_for_a_blank_query_or_no_room_for_any()
    {
        Assert.Empty(Projection.FindText("  ", 10));
        Assert.Empty(Projection.FindText("darkness", 0));
        Assert.Single(Projection.FindText("the", 1));
    }

    [Fact]
    public void Hits_rank_by_where_the_words_are()
    {
        var store = new FakeDossierStore
        {
            Reference = new ReferenceLibrary
            {
                Sections = new[]
                {
                    Block("Scattered", "A pit here, and sand there."),
                    Block("Footing", "The sand pit swallows boots."),
                    Block("Pit of Sand", "Mind your step."),
                    Block("Sand Pit", "Mind your step."),
                },
            },
        };

        IReadOnlyList<TextMatch> hits = PassageIndex.Build(store, null).Find("sand pit", 10);

        Assert.Equal(BeforeHeadingWordsBeforeBodyPhraseBeforeBodyWords, hits.Select(hit => hit.Passage.Key));
    }

    [Fact]
    public void Within_a_rank_the_rules_come_before_the_rooms_and_the_rooms_before_the_rest()
    {
        // A floor's entities are read with the floor, before its rooms, and still rank after them.
        var store = new FakeDossierStore
        {
            Quests = new QuestLog { Quests = new[] { new Quest { Id = "the-lost-hymnal", Title = "The Lost Hymnal", Hook = "Ring the bell again." } } },
            Reference = new ReferenceLibrary { Sections = new[] { Block("The Bell", "Its ringing calms the bell-warden.") } },
        };
        store.Levels.Add(new LevelDossier
        {
            LevelNodeId = Level1,
            Title = "Level 1: The Undercroft",
            Entities = new[] { new Entity { Id = "bell-warden", Name = "Bell-Warden", Kind = EntityKind.Creature, Description = "A gargoyle that keeps the nave." } },
        });
        store.Areas[Landing] = new AreaDossier
        {
            AreaNodeId = Landing,
            Title = "Undercroft Landing",
            Detail = new[] { Block("The Bell", "The bell hangs silent above.") },
        };

        IReadOnlyList<TextMatch> hits = PassageIndex.Build(store, null).Find("bell", 10);

        Assert.Equal(RulesBeforeRoomsBeforeQuests, hits.Select(hit => hit.Passage.Key));
    }

    [Fact]
    public void A_long_passage_shows_the_words_around_the_match_cut_at_words()
    {
        string before = string.Concat(Enumerable.Repeat("dust and old wax ", 12));
        string after = string.Concat(Enumerable.Repeat("cold stone and ash ", 12));
        var store = new FakeDossierStore
        {
            Reference = new ReferenceLibrary { Sections = new[] { Block("The Crypt", before + "the ceiling is ten feet high, " + after) } },
        };

        TextMatch hit = Assert.Single(PassageIndex.Build(store, null).Find("ceiling", 10));

        Assert.StartsWith("…", hit.Snippet, StringComparison.Ordinal);
        Assert.EndsWith("…", hit.Snippet, StringComparison.Ordinal);
        Assert.Contains("the ceiling is ten feet high", hit.Snippet, StringComparison.Ordinal);
        Assert.True(hit.Snippet.Length < hit.Passage.Body.Length / 2, hit.Snippet);

        // Cut at words: the first and last words are whole ones of the body.
        string[] words = hit.Snippet.Trim('…').Split(' ');
        Assert.Contains(words[0], hit.Passage.Body.Split(' '));
        Assert.Contains(words[^1], hit.Passage.Body.Split(' '));
    }

    [Fact]
    public void A_short_passage_is_shown_whole_on_one_line()
    {
        var store = new FakeDossierStore
        {
            Reference = new ReferenceLibrary { Sections = new[] { Block("Doors", "Doors stick.\n\nA DC 10 Strength check opens one.") } },
        };

        TextMatch hit = Assert.Single(PassageIndex.Build(store, null).Find("strength", 10));

        Assert.Equal("Doors stick. A DC 10 Strength check opens one.", hit.Snippet);
    }

    [Fact]
    public void Two_blocks_with_one_heading_get_keys_of_their_own_and_a_block_id_wins_over_its_heading()
    {
        var store = new FakeDossierStore
        {
            Reference = new ReferenceLibrary
            {
                Sections = new[]
                {
                    Block("Doors", "Doors stick."),
                    Block("Doors", "Secret doors are found with a DC 15 check.", secret: true),
                    new DossierBlock { Id = "darkness", Heading = "Darkness Below", Body = "No light." },
                },
            },
        };

        PassageIndex index = PassageIndex.Build(store, null);

        Assert.Equal(DuplicateHeadings, PassageKeys.Rules(store.Reference.Sections));
        Assert.Equal(3, index.Count);
        Assert.True(index.Get("rules/doors-2").Value.Secret);
        Assert.Equal("No light.", index.Get("rules/darkness").Value.Body);
    }

    private static DossierBlock Block(string heading, string body, bool secret = false) =>
        new DossierBlock { Heading = heading, Body = body, Secret = secret };
}
