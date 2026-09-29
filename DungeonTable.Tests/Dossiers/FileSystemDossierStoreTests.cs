using System.Collections.Generic;
using System.IO;
using DungeonTable.Core.Dossier;
using DungeonTable.Infrastructure.Dossiers;

namespace DungeonTable.Tests.Dossiers;

/// <summary>
/// Verifies the file-system dossier store reads committed <c>level-*.json</c> / <c>reference.json</c>
/// documents, indexes areas and levels by node id, honours the camelCase + string-enum contract,
/// and fails soft on missing or corrupt input, using a throwaway temp directory as the root.
/// </summary>
public sealed class FileSystemDossierStoreTests : IDisposable
{
    // The two quests the fixture's single prerequisite offers as alternatives.
    private static readonly string[] PrerequisiteAlternatives = { "the-millers-son", "the-lost-hymnal" };

    // What the creatures-and-exits fixture's lists hold, hoisted out of the assertions (CA1861).
    private static readonly string[] ExitTargets = { "area 6a", "area 3" };
    private static readonly string[] EntityIds = { "bell-warden", "wickfoot-goblins" };

    // The deck fixtures' ids in the order the store should hand them back.
    private static readonly string[] OmensThenRumours = { "omens", "village-rumours" };

    private readonly string tempRoot =
        Path.Combine(Path.GetTempPath(), "dt-dossiers-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(tempRoot))
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Loads_area_level_and_reference_from_disk()
    {
        WriteLevelOne();
        WriteReference();
        var store = new FileSystemDossierStore(tempRoot);

        var area = store.GetArea("data_dossiers_level_1_area_1");
        Assert.True(area.IsValid);
        Assert.Equal(1, area.Value.AreaNumber);
        Assert.Equal("Undercroft Landing", area.Value.Title);
        Assert.Contains("40-foot-square", area.Value.ReadAloud);

        var level = store.GetLevel("data_dossiers_level_1");
        Assert.True(level.IsValid);
        Assert.Equal("Level 1: The Undercroft", level.Value.Title);
        Assert.Equal("Wickfoot goblins", Assert.Single(level.Value.Factions).Heading);

        var reference = store.GetReference();
        Assert.True(reference.IsValid);
        Assert.Equal("Doors and Secret Doors", Assert.Single(reference.Value.Sections).Heading);
    }

    [Fact]
    public void Creatures_exits_entities_and_a_factions_id_load_from_a_level_file()
    {
        Directory.CreateDirectory(tempRoot);
        File.WriteAllText(Path.Combine(tempRoot, "level-1.json"), """
            {
              "level": {
                "levelNodeId": "data_dossiers_level_1",
                "entities": [
                  { "id": "bell-warden", "kind": "creature", "name": "the Bell-Warden", "statBlock": "gargoyle",
                    "description": "Guards the nave.", "secret": true }
                ],
                "factions": [ { "id": "wickfoot-goblins", "heading": "Wickfoot goblins", "body": "A gang." } ]
              },
              "areas": [
                {
                  "areaNodeId": "data_dossiers_level_1_area_6",
                  "creatures": [
                    { "ref": "bandit", "count": 4, "note": "disguised as vampires" },
                    { "ref": "goblin", "count": "1d4+1" },
                    { "ref": "bell-warden", "secret": true }
                  ],
                  "exits": [ { "to": "area 6a" }, { "to": "area 3", "note": "secret door", "secret": true } ]
                }
              ]
            }
            """);
        var store = new FileSystemDossierStore(tempRoot);

        var area = store.GetArea("data_dossiers_level_1_area_6");
        Assert.True(area.IsValid);

        // A count may be written as the number it is; the model keeps it as text beside "1d4+1".
        Assert.Equal(new[] { "4", "1d4+1", string.Empty }, area.Value.Creatures.Select(creature => creature.Count).ToArray());
        Assert.Equal("disguised as vampires", area.Value.Creatures[0].Note);
        Assert.True(area.Value.Creatures[2].Secret);
        Assert.Equal(ExitTargets, area.Value.Exits.Select(exit => exit.To).ToArray());
        Assert.True(area.Value.Exits[1].Secret);

        var level = store.GetLevel("data_dossiers_level_1");
        Assert.True(level.IsValid);
        IReadOnlyList<Entity> entities = level.Value.AllEntities();
        Assert.Equal(EntityIds, entities.Select(entity => entity.Id).ToArray());
        Assert.Equal(EntityKind.Creature, entities[0].Kind);
        Assert.Equal("gargoyle", entities[0].StatBlock);
        Assert.Equal("Guards the nave.", entities[0].Description);
        Assert.True(entities[0].Secret);
        Assert.Equal(EntityKind.Faction, entities[1].Kind);
        Assert.Equal("Wickfoot goblins", entities[1].Name);
        Assert.Equal("A gang.", entities[1].Description);
    }

    [Fact]
    public void Relations_load_from_a_level_file_the_campaign_list_and_npc_contacts()
    {
        Directory.CreateDirectory(tempRoot);
        File.WriteAllText(Path.Combine(tempRoot, "level-1.json"), """
            {
              "level": {
                "levelNodeId": "data_dossiers_level_1",
                "relations": [ { "a": "nib", "rel": "rival", "b": "grask", "note": "An old grudge.", "secret": true } ]
              },
              "areas": []
            }
            """);
        File.WriteAllText(Path.Combine(tempRoot, "relations.json"), """
            { "relations": [ { "a": "brother-aldous", "rel": "plots-against", "b": "grasks-crew" } ] }
            """);
        File.WriteAllText(Path.Combine(tempRoot, "npcs.json"), """
            { "npcs": [ { "id": "wenna-brask", "name": "Wenna Brask",
                          "contacts": [ { "name": "Tam Brask", "ref": "tam-brask", "rel": "protects" } ] } ] }
            """);
        var store = new FileSystemDossierStore(tempRoot);

        var levelDossier = store.GetLevel("data_dossiers_level_1");
        Assert.True(levelDossier.IsValid);
        Relation level = Assert.Single(levelDossier.Value.Relations);
        Assert.Equal("nib", level.A);
        Assert.Equal("rival", level.Rel);
        Assert.Equal("grask", level.B);
        Assert.Equal("An old grudge.", level.Note);
        Assert.True(level.Secret);

        var campaign = store.GetRelations();
        Assert.True(campaign.IsValid);
        Assert.Equal("plots-against", Assert.Single(campaign.Value.Relations).Rel);

        var npcs = store.GetNpcs();
        Assert.True(npcs.IsValid);
        CastMember contact = Assert.Single(Assert.Single(npcs.Value.Npcs).Contacts);
        Assert.Equal("tam-brask", contact.Ref);
        Assert.Equal("protects", contact.Rel);
    }

    [Fact]
    public void An_npc_names_the_stat_block_they_fight_with_and_one_who_names_none_has_none()
    {
        Directory.CreateDirectory(tempRoot);
        File.WriteAllText(Path.Combine(tempRoot, "npcs.json"), """
            { "npcs": [
                { "id": "brother-aldous", "name": "Brother Aldous", "armourClass": 10, "maxHp": 9, "statBlock": "acolyte" },
                { "id": "wenna-brask", "name": "Wenna Brask" }
            ] }
            """);

        IReadOnlyList<NpcDossier> npcs = new FileSystemDossierStore(tempRoot).GetNpcs().Value.Npcs;

        Assert.Equal("acolyte", npcs[0].StatBlock);
        Assert.Equal(10, npcs[0].ArmourClass);
        Assert.Equal(string.Empty, npcs[1].StatBlock);
    }

    [Fact]
    public void No_relations_file_is_no_campaign_relations_rather_than_an_error()
    {
        WriteLevelOne();

        Assert.False(new FileSystemDossierStore(tempRoot).GetRelations().IsValid);
    }

    [Fact]
    public void A_faction_block_without_an_id_is_no_entity()
    {
        var level = new LevelDossier
        {
            Factions = new[]
            {
                new DossierBlock { Heading = "Grask's crew", Body = "Bugbears." },
                new DossierBlock { Id = "  wickfoot-goblins ", Heading = "Wickfoot goblins", Secret = true },
            },
        };

        Entity faction = Assert.Single(level.AllEntities());

        Assert.Equal("wickfoot-goblins", faction.Id);
        Assert.True(faction.Secret);
    }

    [Fact]
    public void A_level_takes_its_floor_number_from_the_file_it_was_read_from()
    {
        WriteLevelOne();
        File.WriteAllText(Path.Combine(tempRoot, "level-12.json"), """{ "level": { "levelNodeId": "twelve" }, "areas": [] }""");
        File.WriteAllText(Path.Combine(tempRoot, "level-bonus.json"), """{ "level": { "levelNodeId": "bonus" }, "areas": [] }""");
        var store = new FileSystemDossierStore(tempRoot);

        Assert.Equal(1, store.GetLevelNumber("data_dossiers_level_1").Value);
        Assert.Equal(12, store.GetLevelNumber("twelve").Value);

        // Named some other way, a level still loads; it just has no number to be reached by.
        Assert.True(store.GetLevel("bonus").IsValid);
        Assert.False(store.GetLevelNumber("bonus").IsValid);
        Assert.False(store.GetLevelNumber("nowhere").IsValid);
        Assert.False(store.GetLevelNumber(" ").IsValid);
    }

    [Fact]
    public void String_enum_kinds_and_secret_flags_round_trip()
    {
        WriteLevelOne();
        var store = new FileSystemDossierStore(tempRoot);

        AreaDossier area = store.GetArea("data_dossiers_level_1_area_1").Value;

        DossierBlock glance = Assert.Single(area.Glance);
        Assert.Equal(DossierBlockKind.Exits, glance.Kind);
        Assert.False(glance.Secret);

        DossierBlock detail = Assert.Single(area.Detail);
        Assert.Equal(DossierBlockKind.Secret, detail.Kind);
        Assert.True(detail.Secret);

        SkillCheck check = Assert.Single(area.SkillChecks);
        Assert.Equal(20, check.Dc);
        Assert.Equal("Wisdom (Perception)", check.Ability);
    }

    [Fact]
    public void Get_level_for_area_resolves_the_owning_level()
    {
        WriteLevelOne();
        var store = new FileSystemDossierStore(tempRoot);

        var level = store.GetLevelForArea("data_dossiers_level_1_area_1");

        Assert.True(level.IsValid);
        Assert.Equal("Level 1: The Undercroft", level.Value.Title);
    }

    [Fact]
    public void Get_level_for_area_also_accepts_a_level_node_id()
    {
        WriteLevelOne();
        var store = new FileSystemDossierStore(tempRoot);

        var level = store.GetLevelForArea("data_dossiers_level_1");

        Assert.True(level.IsValid);
        Assert.Equal("Level 1: The Undercroft", level.Value.Title);
    }

    [Fact]
    public void Get_level_for_an_unknown_or_blank_area_is_invalid()
    {
        WriteLevelOne();
        var store = new FileSystemDossierStore(tempRoot);

        Assert.False(store.GetLevelForArea("data_dossiers_level_1_area_999").IsValid);
        Assert.False(store.GetLevelForArea("   ").IsValid);
    }

    [Fact]
    public void Unknown_area_is_invalid_with_a_message()
    {
        WriteLevelOne();
        var store = new FileSystemDossierStore(tempRoot);

        var result = store.GetArea("data_dossiers_level_1_area_999");

        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Messages);
    }

    [Fact]
    public void Blank_ids_are_invalid()
    {
        var store = new FileSystemDossierStore(tempRoot);

        Assert.False(store.GetArea("").IsValid);
        Assert.False(store.GetArea("   ").IsValid);
        Assert.False(store.GetLevel("").IsValid);
    }

    [Fact]
    public void Missing_reference_is_invalid_rather_than_throwing()
    {
        WriteLevelOne();
        var store = new FileSystemDossierStore(tempRoot);

        Assert.False(store.GetReference().IsValid);
    }

    [Fact]
    public void Quests_round_trip_with_camel_case_and_string_enums()
    {
        WriteQuests();
        var store = new FileSystemDossierStore(tempRoot);

        var result = store.GetQuests();

        Assert.True(result.IsValid);
        Quest quest = Assert.Single(result.Value.Quests);
        Assert.Equal("the-lost-hymnal", quest.Id);
        Assert.Equal(QuestAvailability.Starting, quest.Availability);
        Assert.Equal("Old Hobb, the sexton", quest.GiverLabel);

        QuestBeat beat = Assert.Single(quest.Beats);
        Assert.Equal(1, beat.Ordinal);
        Assert.True(beat.Secret);
        Assert.Equal(QuestTargetKind.Area, beat.Target.Kind);
        Assert.Equal(3, beat.Target.Level);
        Assert.Equal("14c", beat.Target.AreaKey);

        Assert.Equal("A longbow", Assert.Single(quest.Rewards).Heading);
    }

    [Fact]
    public void Quest_prerequisites_round_trip_with_their_alternatives()
    {
        WriteQuests();
        var store = new FileSystemDossierStore(tempRoot);

        Quest quest = store.GetQuests().Value.Quests[0];

        QuestPrerequisite prerequisite = Assert.Single(quest.Prerequisites);
        Assert.Equal(QuestPrerequisiteKind.QuestComplete, prerequisite.Kind);
        Assert.Equal(PrerequisiteAlternatives, prerequisite.AnyOfQuestIds.ToArray());
        Assert.Contains("Prerequisite", prerequisite.Text);
    }

    [Fact]
    public void A_second_quest_document_merges_beside_the_first()
    {
        WriteQuests();
        Directory.CreateDirectory(tempRoot);
        File.WriteAllText(
            Path.Combine(tempRoot, "quests-hotdq.json"),
            """{ "quests": [ { "id": "the-mission", "title": "The Mission", "availability": "side" } ] }""");

        var store = new FileSystemDossierStore(tempRoot);

        IReadOnlyList<Quest> quests = store.GetQuests().Value.Quests;
        Assert.Equal(2, quests.Count);
        Assert.Contains(quests, quest => quest.Id == "the-mission");
    }

    [Fact]
    public void A_duplicate_quest_id_keeps_the_last_documents_content_at_the_first_ones_position()
    {
        // The merge's documented contract, and the branch a second quest file makes reachable:
        // "the-lost-hymnal" is redefined by the later document, so its content is replaced
        // while it stays where the first document put it — the log still reads in book order.
        // "quests_later.json" sorts after "quests.json" ('_' is 0x5F, '.' is 0x2E) under the
        // store's ordinal filename sort, which is what makes "last document" mean something stable
        // rather than filesystem-dependent.
        WriteQuests();
        File.WriteAllText(
            Path.Combine(tempRoot, "quests_later.json"),
            """
            {
              "quests": [
                { "id": "the-mission", "title": "The Mission", "availability": "side" },
                { "id": "the-lost-hymnal", "title": "The Lost Hymnal (revised)", "availability": "future" }
              ]
            }
            """);

        var store = new FileSystemDossierStore(tempRoot);

        IReadOnlyList<Quest> quests = store.GetQuests().Value.Quests;
        Assert.Equal(2, quests.Count);

        // Last document wins on the content...
        Assert.Equal("the-lost-hymnal", quests[0].Id);
        Assert.Equal("The Lost Hymnal (revised)", quests[0].Title);
        Assert.Equal(QuestAvailability.Future, quests[0].Availability);

        // ...but the first appearance keeps its place in the order.
        Assert.Equal("the-mission", quests[1].Id);
    }

    [Fact]
    public void Quest_documents_merge_in_a_stable_order_whatever_the_file_system_returns()
    {
        // Directory.EnumerateFiles does not guarantee an order, so without the store's own sort the
        // winner of a duplicate id could differ between two machines holding identical files.
        // Ordinal by name puts these three in the order: quests-dash, quests.json, quests_under.
        WriteQuests();
        File.WriteAllText(
            Path.Combine(tempRoot, "quests-dash.json"),
            """{ "quests": [ { "id": "the-lost-hymnal", "title": "Loses: '-' sorts before '.'" } ] }""");
        File.WriteAllText(
            Path.Combine(tempRoot, "quests_under.json"),
            """{ "quests": [ { "id": "the-lost-hymnal", "title": "Wins: '_' sorts after '.'" } ] }""");

        var store = new FileSystemDossierStore(tempRoot);

        Quest quest = Assert.Single(store.GetQuests().Value.Quests);
        Assert.Equal("Wins: '_' sorts after '.'", quest.Title);
    }

    [Fact]
    public void A_quest_document_with_a_null_list_is_read_as_an_empty_one()
    {
        // Hand-edited documents clear a list with null as readily as with []. An explicit null
        // overwrites the Core type's Array.Empty default and the deserializer still reports
        // success, so before LenientListConverter this threw out of the constructor and the
        // app did not start at all.
        Directory.CreateDirectory(tempRoot);
        File.WriteAllText(Path.Combine(tempRoot, "quests.json"), """{ "quests": null }""");

        var store = new FileSystemDossierStore(tempRoot);

        Assert.True(store.GetQuests().IsValid);
        Assert.Empty(store.GetQuests().Value.Quests);
    }

    [Fact]
    public void A_null_list_inside_a_quest_is_read_as_an_empty_one()
    {
        Directory.CreateDirectory(tempRoot);
        File.WriteAllText(
            Path.Combine(tempRoot, "quests.json"),
            """
            {
              "quests": [
                {
                  "id": "the-lost-hymnal",
                  "title": "The Lost Hymnal",
                  "beats": null,
                  "prerequisites": null,
                  "rewards": null,
                  "detail": null
                }
              ]
            }
            """);

        var store = new FileSystemDossierStore(tempRoot);

        Quest quest = Assert.Single(store.GetQuests().Value.Quests);
        Assert.Empty(quest.Beats);
        Assert.Empty(quest.Prerequisites);
        Assert.Empty(quest.Rewards);
        Assert.Empty(quest.Detail);
    }

    [Fact]
    public void A_deck_with_a_null_card_list_is_read_as_an_empty_deck()
    {
        // Same shape one layer up: the Rules tab counts deck.Cards on every render, so a null here
        // used to break that whole tab rather than quietly showing no deck.
        Directory.CreateDirectory(tempRoot);
        File.WriteAllText(
            Path.Combine(tempRoot, "deck-omens.json"),
            """{ "id": "omens", "title": "Omens of the Bell", "cards": null }""");

        var store = new FileSystemDossierStore(tempRoot);

        Assert.Empty(Assert.Single(store.AllDecks()).Cards);
    }

    [Fact]
    public void Every_deck_file_loads_in_file_name_order_with_its_rule_and_its_words()
    {
        // Decks are content: the app knows none by name, so all it learns about one is what its
        // document says. File-name order is the Rules tab's order, like the level files.
        Directory.CreateDirectory(tempRoot);
        File.WriteAllText(Path.Combine(tempRoot, "deck-rumours.json"), """
            {
              "id": "village-rumours",
              "title": "Village Rumours",
              "pages": "12",
              "cardNoun": "rumour",
              "drawRule": "kept",
              "drawHint": "Draw a rumour nobody has heard yet",
              "usage": "Draw one when the party asks around the village.",
              "cards": [ { "id": "the-millers-debt", "name": "The Miller's Debt", "body": "He owes more than he says." } ]
            }
            """);
        File.WriteAllText(Path.Combine(tempRoot, "deck-omens.json"), """
            {
              "id": "omens",
              "title": "Omens of the Bell",
              "cardNoun": "prophecy",
              "cardNounPlural": "prophecies",
              "drawRule": "fresh",
              "cards": [
                {
                  "id": "cracked-bell",
                  "name": "Cracked Bell",
                  "subtitle": "Omen of Warning",
                  "effects": [ { "heading": "Bane Effect", "kind": "secret", "body": "The next door sticks." } ]
                }
              ]
            }
            """);

        IReadOnlyList<CardDeck> decks = new FileSystemDossierStore(tempRoot).AllDecks();

        Assert.Equal(OmensThenRumours, decks.Select(deck => deck.Id).ToArray());

        CardDeck omens = decks[0];
        Assert.Equal(DeckDrawRule.Fresh, omens.DrawRule);
        Assert.Equal("prophecy", omens.CardNoun);
        Assert.Equal("prophecies", omens.CardNounPlural);
        DeckCard omen = Assert.Single(omens.Cards);
        Assert.Equal("Omen of Warning", omen.Subtitle);
        Assert.Equal("Bane Effect", Assert.Single(omen.Effects).Heading);

        CardDeck rumours = decks[1];
        Assert.Equal("Village Rumours", rumours.Title);
        Assert.Equal("12", rumours.Pages);
        Assert.Equal(DeckDrawRule.Kept, rumours.DrawRule);
        Assert.Equal("rumour", rumours.CardNoun);
        Assert.Equal("Draw a rumour nobody has heard yet", rumours.DrawHint);
        Assert.Equal("Draw one when the party asks around the village.", rumours.Usage);
        Assert.Equal("The Miller's Debt", Assert.Single(rumours.Cards).Name);
    }

    [Fact]
    public void A_deck_with_no_id_or_bad_json_is_skipped_and_a_repeated_id_keeps_the_later_file()
    {
        // A drawn card is recorded under its deck's id, so a deck with none has nowhere to keep what
        // it dealt. Two files with one id are one deck: the later file wins, in the earlier's place.
        Directory.CreateDirectory(tempRoot);
        File.WriteAllText(Path.Combine(tempRoot, "deck-a.json"), """{ "id": "omens", "title": "First draft" }""");
        File.WriteAllText(Path.Combine(tempRoot, "deck-b.json"), """{ "title": "No id at all", "cards": [ { "id": "x", "name": "X" } ] }""");
        File.WriteAllText(Path.Combine(tempRoot, "deck-c.json"), """{ "id": "village-rumours", "title": "Village Rumours" }""");
        File.WriteAllText(Path.Combine(tempRoot, "deck-d.json"), """{ "id": "omens", "title": "Second draft" }""");
        File.WriteAllText(Path.Combine(tempRoot, "deck-e.json"), "{ not json");
        File.WriteAllText(Path.Combine(tempRoot, "deck-f.json"), """{ "id": "  ", "title": "A blank id" }""");

        IReadOnlyList<CardDeck> decks = new FileSystemDossierStore(tempRoot).AllDecks();

        Assert.Equal(OmensThenRumours, decks.Select(deck => deck.Id).ToArray());
        Assert.Equal("Second draft", decks[0].Title);
    }

    [Fact]
    public void Only_a_file_named_deck_something_is_read_as_a_deck()
    {
        // The name is the convention that makes a document a deck, as level-*.json makes one a
        // floor; a deck-shaped document under any other name is not picked up.
        Directory.CreateDirectory(tempRoot);
        File.WriteAllText(
            Path.Combine(tempRoot, "omens.json"),
            """{ "id": "omens", "title": "Omens of the Bell", "cards": [ { "id": "cracked-bell", "name": "Cracked Bell" } ] }""");

        Assert.Empty(new FileSystemDossierStore(tempRoot).AllDecks());
    }

    [Fact]
    public void No_dossier_directory_means_no_decks()
    {
        Assert.Empty(new FileSystemDossierStore(Path.Combine(tempRoot, "missing")).AllDecks());
    }

    [Fact]
    public void A_level_document_with_null_lists_still_indexes_its_areas()
    {
        Directory.CreateDirectory(tempRoot);
        File.WriteAllText(
            Path.Combine(tempRoot, "level-1.json"),
            """
            {
              "level": { "levelNodeId": "data_dossiers_level_1", "title": "Level 1", "factions": null, "wanderingMonsters": null },
              "areas": [
                { "areaNumber": 1, "title": "Undercroft Landing", "areaNodeId": "data_dossiers_level_1_area_1", "glance": null, "detail": null, "skillChecks": null }
              ]
            }
            """);

        var store = new FileSystemDossierStore(tempRoot);

        Assert.Empty(store.GetLevel("data_dossiers_level_1").Value.Factions);
        AreaDossier area = store.GetArea("data_dossiers_level_1_area_1").Value;
        Assert.Empty(area.Glance);
        Assert.Empty(area.SkillChecks);
    }

    [Fact]
    public void A_level_document_whose_areas_list_is_null_is_skipped_without_throwing()
    {
        Directory.CreateDirectory(tempRoot);
        File.WriteAllText(Path.Combine(tempRoot, "level-2.json"), """{ "areas": null }""");
        WriteLevelOne();

        var store = new FileSystemDossierStore(tempRoot);

        Assert.True(store.GetArea("data_dossiers_level_1_area_1").IsValid);
    }

    [Theory]
    [InlineData("\"level\": null,")]
    [InlineData("\"level\": {},")]
    [InlineData("")]
    public void A_level_file_whose_level_has_no_id_indexes_its_areas_on_no_floor(string level)
    {
        // A hand-edited "level": null threw out of the constructor, and the DM screen answered every
        // request with an error. It is read as a level with no id now, like one written as {} or left
        // out: the areas load, on no floor, and the other level files are untouched.
        WriteLevelOne();
        File.WriteAllText(Path.Combine(tempRoot, "level-2.json"), $$"""
            { {{level}} "areas": [ { "areaNumber": 1, "title": "Ossuary Stair", "areaNodeId": "data_dossiers_level_2_area_1" } ] }
            """);

        var store = new FileSystemDossierStore(tempRoot);

        Assert.Equal(3, store.AllAreas().Count);
        Assert.Equal("Ossuary Stair", store.GetArea("data_dossiers_level_2_area_1").Value.Title);
        Assert.Equal(string.Empty, store.GetLevelForArea("data_dossiers_level_2_area_1").Value.LevelNodeId);
        Assert.Equal("data_dossiers_level_1", Assert.Single(store.AllLevels()).LevelNodeId);
    }

    [Fact]
    public void A_null_entry_in_any_list_of_a_level_file_is_skipped()
    {
        // The constructor read every area's id, so "areas": [ null, … ] threw out of it and took the
        // app down. LenientListConverter drops a null entry from any list, at any depth.
        Directory.CreateDirectory(tempRoot);
        File.WriteAllText(Path.Combine(tempRoot, "level-1.json"), """
            {
              "level": {
                "levelNodeId": "data_dossiers_level_1",
                "factions": [ null, { "heading": "Wickfoot goblins", "body": "A goblin band." } ],
                "entities": [ null ]
              },
              "areas": [
                null,
                {
                  "areaNodeId": "data_dossiers_level_1_area_1",
                  "title": "Undercroft Landing",
                  "glance": [ null, { "heading": "Exits", "kind": "exits", "body": "A tunnel south." } ],
                  "creatures": [ null, { "ref": "goblin", "count": 2 } ],
                  "exits": [ { "to": "area 2" }, null ]
                },
                null
              ]
            }
            """);

        var store = new FileSystemDossierStore(tempRoot);

        AreaDossier area = Assert.Single(store.AllAreas());
        Assert.Equal("Undercroft Landing", area.Title);
        Assert.Equal("Exits", Assert.Single(area.Glance).Heading);
        Assert.Equal("goblin", Assert.Single(area.Creatures).Ref);
        Assert.Equal("area 2", Assert.Single(area.Exits).To);

        LevelDossier level = store.GetLevel("data_dossiers_level_1").Value;
        Assert.Equal("Wickfoot goblins", Assert.Single(level.Factions).Heading);
        Assert.Empty(level.Entities);
    }

    [Fact]
    public void A_null_quest_or_a_null_beat_is_skipped()
    {
        // The quest merge read every quest's id, so one null quest threw out of the constructor.
        Directory.CreateDirectory(tempRoot);
        File.WriteAllText(Path.Combine(tempRoot, "quests.json"), """
            {
              "quests": [
                null,
                {
                  "id": "the-lost-hymnal",
                  "title": "The Lost Hymnal",
                  "prerequisites": [ null ],
                  "beats": [ null, { "id": "find-the-hymnal", "ordinal": 1, "summary": "Find the hymnal." } ]
                }
              ]
            }
            """);

        var store = new FileSystemDossierStore(tempRoot);

        Quest quest = Assert.Single(store.GetQuests().Value.Quests);
        Assert.Equal("the-lost-hymnal", quest.Id);
        Assert.Empty(quest.Prerequisites);
        Assert.Equal("find-the-hymnal", Assert.Single(quest.Beats).Id);
    }

    [Fact]
    public void A_null_card_npc_or_character_is_skipped()
    {
        // None of these is read by the constructor, but the link index reads every NPC's and every
        // character's id as the DM screen opens, and the Rules tab every card: a null among them broke
        // the screen or the tab instead.
        Directory.CreateDirectory(tempRoot);
        File.WriteAllText(Path.Combine(tempRoot, "deck-omens.json"), """
            { "id": "omens", "title": "Omens of the Bell", "cards": [ null, { "id": "cracked-bell", "name": "Cracked Bell" } ] }
            """);
        File.WriteAllText(Path.Combine(tempRoot, "npcs.json"), """
            { "npcs": [ null, { "id": "wenna-brask", "name": "Wenna Brask" } ] }
            """);
        File.WriteAllText(Path.Combine(tempRoot, "characters.json"), """
            { "characters": [ { "id": "maren", "name": "Maren" }, null ] }
            """);

        var store = new FileSystemDossierStore(tempRoot);

        Assert.Equal("cracked-bell", Assert.Single(Assert.Single(store.AllDecks()).Cards).Id);
        Assert.Equal("wenna-brask", Assert.Single(store.GetNpcs().Value.Npcs).Id);
        Assert.Equal("maren", Assert.Single(store.GetParty().Value.Characters).Id);
    }

    [Fact]
    public void Missing_quests_are_invalid_rather_than_throwing()
    {
        WriteLevelOne();
        var store = new FileSystemDossierStore(tempRoot);

        Assert.False(store.GetQuests().IsValid);
    }

    [Fact]
    public void A_corrupt_level_file_is_skipped_and_others_still_load()
    {
        Directory.CreateDirectory(tempRoot);
        File.WriteAllText(Path.Combine(tempRoot, "level-2.json"), "{ not valid json ");
        WriteLevelOne();

        var store = new FileSystemDossierStore(tempRoot);

        Assert.True(store.GetArea("data_dossiers_level_1_area_1").IsValid);
    }

    [Fact]
    public void An_empty_or_missing_root_serves_nothing_without_throwing()
    {
        var store = new FileSystemDossierStore(tempRoot);

        Assert.False(store.GetArea("data_dossiers_level_1_area_1").IsValid);
        Assert.False(store.GetLevel("data_dossiers_level_1").IsValid);
        Assert.False(store.GetReference().IsValid);
    }

    private void WriteLevelOne()
    {
        Directory.CreateDirectory(tempRoot);
        const string json = """
        {
          "level": {
            "levelNodeId": "data_dossiers_level_1",
            "title": "Level 1: The Undercroft",
            "designedFor": "Four 5th-level characters.",
            "whatDwellsHere": "A goblin band and Grask's bugbears.",
            "factions": [
              { "heading": "Wickfoot goblins", "kind": "lore", "secret": false, "body": "A goblin band." }
            ],
            "wanderingMonsters": []
          },
          "areas": [
            {
              "areaNumber": 1,
              "title": "Undercroft Landing",
              "areaNodeId": "data_dossiers_level_1_area_1",
              "pages": "15",
              "readAloud": "A dark, 40-foot-square room.",
              "glance": [
                { "heading": "Exits", "kind": "exits", "secret": false, "body": "A tunnel south." }
              ],
              "detail": [
                { "heading": "One-Way Secret Door", "kind": "secret", "secret": true, "body": "In the north wall." }
              ],
              "skillChecks": [
                { "ability": "Wisdom (Perception)", "dc": 20, "purpose": "Hear the bandit." }
              ]
            },
            {
              "areaNumber": 2,
              "title": "Demon Reliefs (Level 1, Area 2a)",
              "areaNodeId": "data_dossiers_level_1_area_2a",
              "pages": "16",
              "readAloud": "",
              "glance": [],
              "detail": [],
              "skillChecks": []
            }
          ]
        }
        """;
        File.WriteAllText(Path.Combine(tempRoot, "level-1.json"), json);
    }

    private void WriteQuests()
    {
        Directory.CreateDirectory(tempRoot);
        const string json = """
        {
          "quests": [
            {
              "id": "the-lost-hymnal",
              "title": "The Lost Hymnal",
              "availability": "starting",
              "pages": "8",
              "giverLabel": "Old Hobb, the sexton",
              "giverNodeId": "node_volo",
              "hook": "Volo buys a round and tells a story.",
              "prerequisites": [
                {
                  "kind": "questComplete",
                  "anyOfQuestIds": [ "the-millers-son", "the-lost-hymnal" ],
                  "characterLevel": 0,
                  "text": "Prerequisite: Complete either quest"
                }
              ],
              "beats": [
                {
                  "id": "find-the-throne",
                  "ordinal": 1,
                  "summary": "Find the throne.",
                  "detail": "A chipped alabaster throne.",
                  "secret": true,
                  "target": {
                    "kind": "area",
                    "level": 3,
                    "areaKey": "14c",
                    "placeLabel": "Hall of Stone",
                    "nodeId": "data_dossiers_level_3_area_14c"
                  }
                }
              ],
              "rewards": [
                { "heading": "A longbow", "kind": "loot", "secret": false, "body": "And twenty silvered arrows." }
              ],
              "detail": []
            }
          ]
        }
        """;
        File.WriteAllText(Path.Combine(tempRoot, "quests.json"), json);
    }

    private void WriteReference()
    {
        Directory.CreateDirectory(tempRoot);
        const string json = """
        {
          "sections": [
            { "heading": "Doors and Secret Doors", "kind": "lore", "secret": false, "body": "DC 20 to find." }
          ]
        }
        """;
        File.WriteAllText(Path.Combine(tempRoot, "reference.json"), json);
    }
}
