using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using DungeonTable.Core.Art;
using DungeonTable.Core.Battle;
using DungeonTable.Core.Dossier;
using DungeonTable.Core.Maps;
using DungeonTable.Core.Stats;
using DungeonTable.Infrastructure.Art;
using DungeonTable.Infrastructure.Books;
using DungeonTable.Infrastructure.Dossiers;
using DungeonTable.Infrastructure.Maps;
using DungeonTable.Infrastructure.Rosters;
using DungeonTable.Infrastructure.Stats;

namespace DungeonTable.Tests.Dossiers;

/// <summary>
/// Documents written by hand, and above all by an LLM, say "nothing here" with <c>null</c>. In every
/// store, a <c>null</c> reads as if the field were left out, and a <c>null</c> entry in a list is
/// dropped: text is empty, a number, flag or enum is its default, an object keeps its empty start.
/// </summary>
public sealed class HandWrittenJsonTests : IDisposable
{
    private readonly string tempRoot = Path.Combine(Path.GetTempPath(), "dt-json-" + Guid.NewGuid().ToString("N"));

    public HandWrittenJsonTests() => Directory.CreateDirectory(tempRoot);

    public void Dispose()
    {
        if (Directory.Exists(tempRoot))
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Nulls_in_a_level_file_read_as_if_left_out()
    {
        // An area's null title answered the DM screen with an error (its label is read as the screen
        // opens), and a null number, flag or kind lost the whole file.
        Write("level-1.json", """
            {
              "level": { "levelNodeId": "data_dossiers_level_1", "title": null, "designedFor": null, "entities": [ { "id": "bell-warden", "kind": null, "name": null, "secret": null } ] },
              "areas": [
                {
                  "areaNodeId": "data_dossiers_level_1_area_1",
                  "title": null,
                  "areaNumber": null,
                  "readAloud": null,
                  "glance": [ { "heading": null, "body": null, "kind": null, "secret": null } ],
                  "skillChecks": [ { "ability": null, "dc": null, "purpose": null } ],
                  "creatures": [ { "ref": "goblin", "count": null, "note": null, "secret": null } ],
                  "exits": [ { "to": null, "note": null } ],
                  "pages": null
                }
              ]
            }
            """);

        var store = new FileSystemDossierStore(tempRoot);

        AreaDossier area = store.GetArea("data_dossiers_level_1_area_1").Value;
        Assert.Equal(string.Empty, area.Title);
        Assert.Equal(0, area.AreaNumber);
        Assert.Equal(string.Empty, area.ReadAloud);
        DossierBlock glance = Assert.Single(area.Glance);
        Assert.Equal(string.Empty, glance.Heading);
        Assert.Equal(DossierBlockKind.None, glance.Kind);
        Assert.False(glance.Secret);
        Assert.Equal(0, Assert.Single(area.SkillChecks).Dc);
        Assert.Equal(string.Empty, Assert.Single(area.Creatures).Count);
        Assert.Equal(string.Empty, Assert.Single(area.Exits).To);

        Entity entity = Assert.Single(store.GetLevel("data_dossiers_level_1").Value.Entities);
        Assert.Equal(EntityKind.None, entity.Kind);
        Assert.Equal(string.Empty, entity.Name);
    }

    [Fact]
    public void Nulls_in_the_people_and_the_quests_read_as_if_left_out()
    {
        // An NPC's null id or name answered the DM screen with an error: the link index reads every one
        // as the screen opens. A null armour class lost the whole roster.
        Write("npcs.json", """{ "summary": null, "npcs": [ { "id": null, "name": null, "armourClass": null, "maxHp": null, "knows": [ { "heading": "Tam", "body": null } ], "contacts": [ { "name": null, "ref": null } ] } ] }""");
        Write("characters.json", """{ "characters": [ { "id": "maren", "name": "Maren", "level": null, "saveDc": null } ] }""");
        Write("quests.json", """
            {
              "quests": [
                {
                  "id": "find-tam",
                  "title": null,
                  "availability": null,
                  "prerequisites": [ { "kind": null, "anyOfQuestIds": [ "the-lost-hymnal", null ], "characterLevel": null } ],
                  "beats": [ { "id": "one", "ordinal": null, "target": null }, { "id": "two", "target": { "kind": "area", "level": null, "nodeId": null } } ]
                }
              ]
            }
            """);
        Write("deck-omens.json", """{ "id": "omens", "drawRule": null, "cardNoun": null, "cards": [ { "id": "cracked-bell", "name": null } ] }""");

        var store = new FileSystemDossierStore(tempRoot);

        NpcDossier npc = Assert.Single(store.GetNpcs().Value.Npcs);
        Assert.Equal(string.Empty, npc.Id);
        Assert.Equal(string.Empty, npc.Name);
        Assert.Equal(0, npc.ArmourClass);
        Assert.Equal(string.Empty, Assert.Single(npc.Knows).Body);
        Assert.Equal(string.Empty, Assert.Single(npc.Contacts).Ref);
        Assert.Equal(0, Assert.Single(store.GetParty().Value.Characters).Level);

        Quest quest = Assert.Single(store.GetQuests().Value.Quests);
        Assert.Equal(string.Empty, quest.Title);
        Assert.Equal(QuestAvailability.None, quest.Availability);

        // A null quest id in a list is dropped, not read as an empty id that names no quest.
        Assert.Equal("the-lost-hymnal", Assert.Single(Assert.Single(quest.Prerequisites).AnyOfQuestIds));
        Assert.NotNull(quest.Beats[0].Target);
        Assert.Equal(QuestTargetKind.None, quest.Beats[0].Target.Kind);
        Assert.Equal(string.Empty, quest.Beats[1].Target.NodeId);

        CardDeck deck = Assert.Single(store.AllDecks());
        Assert.Equal(DeckDrawRule.None, deck.DrawRule);
        Assert.Equal(string.Empty, Assert.Single(deck.Cards).Name);
    }

    [Fact]
    public void Nulls_and_numbers_in_a_stat_block_read_as_if_left_out()
    {
        Write("monsters.json", """
            [
              {
                "nodeId": "grask", "name": "Grask", "alignment": null, "armourClass": null, "challengeRating": 2,
                "abilities": null, "spellcasting": null, "speeds": [ { "kind": null, "feet": 30 } ],
                "actions": [ { "name": "Morningstar", "text": null, "attacks": [ { "toHit": null, "damageDice": null } ] } ]
              }
            ]
            """);

        var library = new FileSystemStatLibrary(tempRoot);

        StatBlock block = library.GetMonster("grask").Value;
        Assert.Equal(string.Empty, block.Alignment);
        Assert.Equal(0, block.ArmourClass);
        Assert.Equal("2", block.ChallengeRating);
        Assert.NotNull(block.Abilities);
        Assert.NotNull(block.Spellcasting);
        Assert.Equal(SpeedKind.None, Assert.Single(block.Speeds).Kind);
        Assert.Equal(string.Empty, Assert.Single(block.Actions).Text);
    }

    [Fact]
    public async Task A_null_region_or_corner_in_a_map_is_dropped_and_a_null_label_is_blank()
    {
        // A null region took the DM screen down, and a null corner lost the whole map's notes.
        string folder = Path.Combine(tempRoot, "demo");
        Directory.CreateDirectory(folder);
        await File.WriteAllTextAsync(Path.Combine(folder, "undercroft.regions.json"), """
            {
              "mapId": "undercroft", "adventure": "demo", "levelName": null,
              "regions": [
                null,
                { "regionId": "r1", "label": null, "graphNodeId": null, "polygon": [ { "x": 0, "y": 0 }, null, { "x": 4, "y": 0 }, { "x": 4, "y": 4 } ] }
              ],
              "features": [ { "featureId": "f1", "kind": null, "label": null, "position": { "x": null, "y": 2 } } ],
              "objects": [ null, { "objectId": "door-1", "kind": "door", "isSecret": null } ]
            }
            """, CancellationToken.None);

        var store = new FileSystemMapStore(tempRoot);
        MapDefinition map = (await store.LoadAsync("undercroft", CancellationToken.None)).Value;

        Assert.Equal(string.Empty, map.LevelName);
        Region region = Assert.Single(map.Regions);
        Assert.Equal(string.Empty, region.Label);
        Assert.Equal(string.Empty, region.GraphNodeId);
        Assert.Equal(3, region.Polygon.Count);
        FeatureMarker feature = Assert.Single(map.Features);
        Assert.Equal(new MapPoint(0, 2), feature.Position);
        Assert.False(Assert.Single(map.Objects).IsSecret);
    }

    [Fact]
    public void A_null_party_or_ally_member_is_dropped_and_their_null_fields_are_blank()
    {
        // A null member took the Battle tab down.
        Write("party.json", """{ "members": [ null, { "name": "Maren", "playerName": null, "level": null } ] }""");
        Write("allies.json", """{ "members": [ { "name": "Pip", "statBlockNodeId": null, "maxHp": null }, null ] }""");

        PartyMember member = Assert.Single(new FileSystemPartyRoster(tempRoot).GetParty().Value.Members);
        Assert.Equal("Maren", member.Name);
        Assert.Equal(string.Empty, member.PlayerName);
        Assert.Equal(0, member.Level);

        AllyMember ally = Assert.Single(new FileSystemAllyRoster(tempRoot).GetAllies().Value.Members);
        Assert.Equal(string.Empty, ally.StatBlockNodeId);
    }

    [Fact]
    public void A_null_page_in_the_book_index_is_none_and_a_page_written_as_text_still_reads()
    {
        Write("almanac.json", """
            {
              "book": "almanac", "title": null,
              "entries": [ { "id": "the-old-mill", "name": "The Old Mill", "page": null }, { "id": "the-ford", "name": "The Ford", "page": "12" } ]
            }
            """);

        BookIndex book = Assert.Single(new FileSystemBookIndexStore(tempRoot).AllBooks());

        Assert.Equal(string.Empty, book.Title);
        Assert.Equal(0, book.Entries[0].Page);
        Assert.Equal(12, book.Entries[1].Page);
    }

    [Fact]
    public void A_null_title_or_tag_in_the_art_catalogue_is_blank_or_dropped()
    {
        Write("catalogue.json", """{ "images": [ { "id": "bell-warden", "file": "demo/bell-warden.webp", "title": null, "kind": null, "tags": [ "gargoyle", null ], "width": null } ] }""");

        ArtImage image = Assert.Single(new FileSystemArtCatalogue(tempRoot).All());

        Assert.Equal(string.Empty, image.Title);
        Assert.Equal(ArtKind.None, image.Kind);
        Assert.Equal("gargoyle", Assert.Single(image.Tags));
    }

    [Fact]
    public async Task The_room_editor_writes_the_sample_maps_notes_byte_for_byte_as_before()
    {
        // The map store writes with the options it reads with, so the new reading rules must not
        // change a byte of what the Room Editor saves.
        string sample = Path.Combine(SamplePack.Maps, SamplePack.Adventure, SamplePack.MapId + ".regions.json");
        MapDefinition map = JsonSerializer.Deserialize<MapDefinition>(await File.ReadAllTextAsync(sample, CancellationToken.None), BeforeWithIndent);

        await new FileSystemMapStore(tempRoot).SaveAsync(map, CancellationToken.None);

        string written = await File.ReadAllTextAsync(Path.Combine(tempRoot, SamplePack.Adventure, SamplePack.MapId + ".regions.json"), CancellationToken.None);
        Assert.Equal(JsonSerializer.Serialize(map, BeforeWithIndent), written);
    }

    [Fact]
    public void The_roster_editors_write_the_sample_rosters_byte_for_byte_as_before()
    {
        Party party = JsonSerializer.Deserialize<Party>(File.ReadAllText(Path.Combine(SamplePack.Root, "data", "party.json")), BeforeWithIndent);
        AllyRoster allies = JsonSerializer.Deserialize<AllyRoster>(File.ReadAllText(Path.Combine(SamplePack.Root, "data", "allies.json")), BeforeWithIndent);

        Assert.True(new FileSystemPartyRoster(tempRoot).Save(party).IsValid);
        Assert.True(new FileSystemAllyRoster(tempRoot).Save(allies).IsValid);

        Assert.Equal(JsonSerializer.Serialize(party, BeforeWithIndent), File.ReadAllText(Path.Combine(tempRoot, "party.json")));
        Assert.Equal(JsonSerializer.Serialize(allies, BeforeWithIndent), File.ReadAllText(Path.Combine(tempRoot, "allies.json")));
    }

    [Fact]
    public void The_uploads_catalogue_is_written_byte_for_byte_as_before()
    {
        var catalogue = new ArtCatalogue
        {
            Images = new[]
            {
                new ArtImage
                {
                    Id = "upload-the-mill", File = "uploads/upload-the-mill.webp", Title = "The mill's wheel", Kind = ArtKind.Handout,
                    Origin = ArtOrigin.Upload, Tags = new[] { "upload" }, Width = 640, Height = 480,
                },
            },
        };

        Assert.Equal(JsonSerializer.Serialize(catalogue, BeforeWithIndent), JsonSerializer.Serialize(catalogue, FileSystemArtCatalogue.Options));
    }

    // What the map store, the rosters and the art catalogue wrote with before a null read as left out.
    private static readonly JsonSerializerOptions BeforeWithIndent = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private void Write(string name, string json) => File.WriteAllText(Path.Combine(tempRoot, name), json);
}
