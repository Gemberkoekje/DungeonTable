using System.Collections.Generic;
using System.Threading.Tasks;
using DungeonTable.ContentTests.Rules;

namespace DungeonTable.ContentTests.RuleTests;

/// <summary>
/// Proves the document rules on real files: the listing finds documents by the names the app reads,
/// sets aside the files it would not read, and a document that does not load says why.
/// </summary>
public sealed class DocumentAuditTests
{
    private static readonly string[] Unread =
    {
        "data/dossiers/Level-2.json", "data/dossiers/npc.json", "data/statblocks/monster.json", "data/roster.json",
    };

    private static readonly string[] DossierNames = { "level-*.json", "npcs.json" };

    private static readonly string[] NullEntriesInALevelFile =
    {
        "level.factions[1] is null, so the app drops it.",
        "areas[0] is null, so the app drops it.",
        "areas[1].creatures[0] is null, so the app drops it.",
        "areas[1].creatures[1] is null, so the app drops it.",
    };

    [Theory]
    [InlineData("level-1.json", "level-*.json", true)]
    [InlineData("level-12.json", "level-*.json", true)]
    [InlineData("Level-1.json", "level-*.json", false)]
    [InlineData("level-1.JSON", "level-*.json", false)]
    [InlineData("quests.json", "quests*.json", true)]
    [InlineData("quests-side.json", "quests*.json", true)]
    [InlineData("npcs.json", "npcs.json", true)]
    [InlineData("npc.json", "npcs.json", false)]
    [InlineData("NPCS.json", "npcs.json", false)]
    public void A_name_matches_a_pattern_exactly_as_the_linux_server_matches_it(string name, string pattern, bool matches)
    {
        Assert.Equal(matches, ContentDocuments.Matches(name, pattern));
    }

    [Fact]
    public void The_listing_finds_each_document_under_the_name_its_store_reads()
    {
        using var content = new TempContent();
        content.Write("data/dossiers/level-1.json", "{}");
        content.Write("data/dossiers/npcs.json", "{}");
        content.Write("data/dossiers/deck-omens.json", "{}");
        content.Write("data/dossiers/quests-side.json", "{}");
        content.Write("data/dossiers/notes.txt", "not a document");
        content.Write("data/statblocks/monsters-srd.json", "[]");
        content.Write("data/statblocks/spells.json", "[]");
        content.Write("data/book-index/almanac.json", "{}");
        content.Write("data/art/catalogue.json", "{}");
        content.Write("data/party.json", "{}");
        content.Write("data/allies.json", "{}");
        content.Write("Maps/demo/map.ds", "{}");
        content.Write("Maps/demo/map.regions.json", "{}");
        content.Write("Maps/demo/trace.png", "not a document");

        DocumentListing listing = ContentDocuments.List(content.Root, content.Start);

        Assert.Equal(
            new[]
            {
                ("data/dossiers/deck-omens.json", DocumentKind.Deck),
                ("data/dossiers/level-1.json", DocumentKind.Level),
                ("data/dossiers/npcs.json", DocumentKind.Npcs),
                ("data/dossiers/quests-side.json", DocumentKind.Quests),
                ("data/statblocks/monsters-srd.json", DocumentKind.Monsters),
                ("data/statblocks/spells.json", DocumentKind.Spells),
                ("data/book-index/almanac.json", DocumentKind.BookIndex),
                ("data/art/catalogue.json", DocumentKind.ArtCatalogue),
                ("data/allies.json", DocumentKind.AllyRoster),
                ("data/party.json", DocumentKind.PartyRoster),
                ("Maps/demo/map.ds", DocumentKind.MapDrawing),
                ("Maps/demo/map.regions.json", DocumentKind.MapRegions),
            },
            listing.Documents.Select(document => (document.Name, document.Kind)));
        Assert.Empty(listing.Unread);
    }

    [Fact]
    public void A_json_file_the_app_reads_under_no_name_is_set_aside()
    {
        using var content = new TempContent();
        content.Write("data/dossiers/npc.json", "{}");
        content.Write("data/dossiers/Level-2.json", "{}");
        content.Write("data/statblocks/monster.json", "[]");
        content.Write("data/roster.json", "{}");
        content.Write("data/art/catalogue.json", "{}");
        content.Write("Maps/.keep", string.Empty);

        DocumentListing listing = ContentDocuments.List(content.Root, content.Start);

        Assert.Equal(
            Unread,
            listing.Unread.Select(file => file.Name));
        Assert.Single(listing.Documents);
    }

    [Fact]
    public void An_unread_file_is_reported_with_the_names_its_folder_reads_and_a_casing_slip_is_named()
    {
        IReadOnlyList<string> faults = DocumentAudit.UnreadFiles(new[]
        {
            new UnreadFile("data/dossiers/npc.json", DossierNames),
            new UnreadFile("data/dossiers/Level-2.json", DossierNames),
        });

        Assert.Equal(2, faults.Count);
        Assert.Contains("data/dossiers/npc.json", faults[0], StringComparison.Ordinal);
        Assert.Contains("level-*.json, npcs.json", faults[0], StringComparison.Ordinal);
        Assert.DoesNotContain("lower case", faults[0], StringComparison.Ordinal);
        Assert.Contains("lower case", faults[1], StringComparison.Ordinal);
    }

    [Fact]
    public void A_kind_of_content_the_root_lacks_has_no_documents_rather_than_stopping_the_listing()
    {
        using var content = new TempContent();
        content.Write("data/dossiers/level-1.json", "{}");

        DocumentListing listing = ContentDocuments.List(content.Root, content.Start);

        Assert.Equal("data/dossiers/level-1.json", Assert.Single(listing.Documents).Name);
    }

    [Theory]
    [InlineData("data/dossiers/level-1.json", DocumentKind.Level, "{ \"level\": { \"levelNodeId\": \"floor-1\" }, \"areas\": [] }")]
    [InlineData("data/dossiers/npcs.json", DocumentKind.Npcs, "{ \"npcs\": [ { \"id\": \"wenna-brask\", }, ], } // trailing commas and comments read")]
    [InlineData("data/statblocks/monsters.json", DocumentKind.Monsters, "[ { \"nodeId\": \"goblin\" } ]")]
    [InlineData("data/party.json", DocumentKind.PartyRoster, "{ \"members\": [] }")]

    // What an LLM writes for "nothing here": null for a text, a number, a flag, an enum, an object or a
    // list entry, and a number where text belongs. The app reads each as if it were left out.
    [InlineData("data/dossiers/npcs.json", DocumentKind.Npcs, "{ \"npcs\": [ { \"id\": \"wenna-brask\", \"name\": null, \"armourClass\": null, \"knows\": [ { \"heading\": \"Tam\", \"secret\": null, \"kind\": null } ] } ] }")]
    [InlineData("data/dossiers/quests.json", DocumentKind.Quests, "{ \"quests\": [ { \"id\": \"find-tam\", \"availability\": null, \"prerequisites\": [ { \"kind\": \"questComplete\", \"anyOfQuestIds\": [ null ] } ], \"beats\": [ { \"id\": \"one\", \"target\": null } ] } ] }")]
    [InlineData("data/statblocks/monsters.json", DocumentKind.Monsters, "[ { \"nodeId\": \"goblin\", \"challengeRating\": 2, \"alignment\": null, \"abilities\": null, \"spellcasting\": null } ]")]
    [InlineData("Maps/demo/map.regions.json", DocumentKind.MapRegions, "{ \"mapId\": \"map\", \"regions\": [ null, { \"regionId\": \"r1\", \"label\": null, \"polygon\": [ { \"x\": 0, \"y\": 0 }, null ] } ] }")]
    [InlineData("data/party.json", DocumentKind.PartyRoster, "{ \"members\": [ null, { \"name\": \"Maren\", \"playerName\": null, \"level\": null } ] }")]
    public async Task A_document_the_app_reads_loads(string path, DocumentKind kind, string json)
    {
        using var content = new TempContent();
        content.Write(path, json);

        Assert.Equal(string.Empty, await DocumentAudit.LoadFaultAsync(content.Document(path, kind)));
    }

    [Theory]
    [InlineData("data/dossiers/level-1.json", DocumentKind.Level, "{ \"level\": { \"levelNodeId\": \"floor-1\" ")]
    [InlineData("data/dossiers/npcs.json", DocumentKind.Npcs, "[]")]
    [InlineData("data/dossiers/deck-omens.json", DocumentKind.Deck, "null")]
    [InlineData("data/statblocks/monsters.json", DocumentKind.Monsters, "{ \"nodeId\": \"goblin\" }")]
    [InlineData("data/statblocks/monsters.json", DocumentKind.Monsters, "[ { \"nodeId\": \"goblin\", \"armourClass\": \"high\" } ]")]
    [InlineData("data/art/catalogue.json", DocumentKind.ArtCatalogue, "{ \"images\": { } }")]
    [InlineData("Maps/demo/map.regions.json", DocumentKind.MapRegions, "{ \"regions\": 4 }")]
    [InlineData("data/allies.json", DocumentKind.AllyRoster, "{ \"members\": [ { \"armourClass\": \"high\" } ] }")]
    public async Task A_document_that_does_not_load_says_why(string path, DocumentKind kind, string json)
    {
        using var content = new TempContent();
        content.Write(path, json);

        string fault = await DocumentAudit.LoadFaultAsync(content.Document(path, kind));

        Assert.NotEqual(string.Empty, fault);
    }

    [Fact]
    public async Task A_drawing_that_is_no_dungeon_scrawl_file_does_not_load()
    {
        using var content = new TempContent();
        content.Write("Maps/demo/map.ds", "this is not a drawing");

        Assert.NotEqual(string.Empty, await DocumentAudit.LoadFaultAsync(content.Document("Maps/demo/map.ds", DocumentKind.MapDrawing)));
    }

    [Fact]
    public void A_null_entry_in_any_list_at_any_depth_is_named_by_where_it_is()
    {
        IReadOnlyList<string> faults = DocumentAudit.NullEntries("""
            {
              "level": { "levelNodeId": "floor-1", "factions": [ { "heading": "Goblins" }, null ] },
              "areas": [ null, { "areaNodeId": "a-1", "creatures": [ null, null ], "exits": [ { "to": "area 2" } ] } ]
            }
            """);

        Assert.Equal(NullEntriesInALevelFile, faults);
    }

    [Fact]
    public void A_null_entry_in_a_document_that_is_a_list_is_named_by_its_index()
    {
        string fault = Assert.Single(DocumentAudit.NullEntries("""[ { "nodeId": "goblin", "senses": [ "darkvision 60 ft." ] }, null ]"""));

        Assert.Equal("[1] is null, so the app drops it.", fault);
    }

    [Fact]
    public void A_null_list_or_a_null_value_is_no_null_entry()
    {
        // Both read as nothing written, as [] and "" would. The comment and the trailing comma are read
        // the way the stores read them.
        Assert.Empty(DocumentAudit.NullEntries("""
            {
              "level": null,
              "areas": [ { "areaNodeId": "a-1", "title": null, "creatures": null }, ],
              // "exits": [ null ]
            }
            """));
    }
}
