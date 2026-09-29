using System.IO;
using DungeonTable.Core.Stats;
using DungeonTable.Infrastructure.Stats;

namespace DungeonTable.Tests.Stats;

/// <summary>
/// Verifies the file-system stat library reads committed <c>monsters*.json</c> / <c>spells*.json</c>
/// documents, merges them in ordinal file-name order, indexes both by node id, honours the
/// camelCase + string-enum contract, and fails soft on missing or corrupt input, using a throwaway
/// temp directory as the root.
/// </summary>
public sealed class FileSystemStatLibraryTests : IDisposable
{
    private readonly string tempRoot =
        Path.Combine(Path.GetTempPath(), "dt-statblocks-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(tempRoot))
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Loads_a_monster_and_a_spell_from_disk()
    {
        WriteMonsters();
        WriteSpells();
        var library = new FileSystemStatLibrary(tempRoot);

        var monster = library.GetMonster("data_statblocks_monsters_bugbear");
        Assert.True(monster.IsValid);
        Assert.Equal("Bugbear", monster.Value.Name);
        Assert.Equal(16, monster.Value.ArmourClass);
        Assert.Equal(SpeedKind.Walk, Assert.Single(monster.Value.Speeds).Kind);
        Assert.Equal(AttackKind.MeleeWeapon, Assert.Single(monster.Value.Actions).Attacks[0].Kind);

        var spell = library.GetSpell("data_statblocks_spells_fireball");
        Assert.True(spell.IsValid);
        Assert.Equal("Fireball", spell.Value.Name);
        Assert.Equal(3, spell.Value.Level);
        Assert.False(spell.Value.Concentration);
    }

    [Fact]
    public void Every_block_and_spell_is_listed_for_callers_that_index_the_whole_set()
    {
        WriteMonsters();
        WriteSpells();
        var library = new FileSystemStatLibrary(tempRoot);

        Assert.Equal("Bugbear", Assert.Single(library.AllMonsters()).Name);
        Assert.Equal("Fireball", Assert.Single(library.AllSpells()).Name);
    }

    [Fact]
    public void A_missing_root_lists_nothing()
    {
        var library = new FileSystemStatLibrary(tempRoot);

        Assert.Empty(library.AllMonsters());
        Assert.Empty(library.AllSpells());
    }

    [Fact]
    public void Has_monster_reflects_the_index()
    {
        WriteMonsters();
        var library = new FileSystemStatLibrary(tempRoot);

        Assert.True(library.HasMonster("data_statblocks_monsters_bugbear"));
        Assert.False(library.HasMonster("data_statblocks_monsters_no_such_monster"));
        Assert.False(library.HasMonster(""));
    }

    [Fact]
    public void Search_monsters_ranks_the_exact_name_first()
    {
        WriteMonsters();
        var library = new FileSystemStatLibrary(tempRoot);

        var matches = library.SearchMonsters("Bugbear", 10);

        Assert.NotEmpty(matches);
        Assert.Equal("data_statblocks_monsters_bugbear", matches[0].Id);
        Assert.Equal("monster", matches[0].Category);
    }

    [Fact]
    public void Search_monsters_respects_the_limit_and_ignores_a_blank_query()
    {
        WriteMonsters();
        var library = new FileSystemStatLibrary(tempRoot);

        Assert.True(library.SearchMonsters("bug", 0).Count == 0);
        Assert.Empty(library.SearchMonsters("", 10));
        Assert.Empty(library.SearchMonsters("   ", 10));
    }

    [Fact]
    public void Unknown_monster_and_spell_are_invalid_with_a_message()
    {
        WriteMonsters();
        var library = new FileSystemStatLibrary(tempRoot);

        var monster = library.GetMonster("data_statblocks_monsters_no_such_monster");
        Assert.False(monster.IsValid);
        Assert.NotEmpty(monster.Messages);

        var spell = library.GetSpell("data_statblocks_spells_no_such_spell");
        Assert.False(spell.IsValid);
        Assert.NotEmpty(spell.Messages);
    }

    [Fact]
    public void Blank_ids_are_invalid()
    {
        var library = new FileSystemStatLibrary(tempRoot);

        Assert.False(library.GetMonster("").IsValid);
        Assert.False(library.GetMonster("   ").IsValid);
        Assert.False(library.GetSpell("").IsValid);
    }

    [Fact]
    public void A_corrupt_spells_file_is_skipped_and_monsters_still_load()
    {
        Directory.CreateDirectory(tempRoot);
        File.WriteAllText(Path.Combine(tempRoot, "spells.json"), "[ not valid json ");
        WriteMonsters();

        var library = new FileSystemStatLibrary(tempRoot);

        Assert.True(library.GetMonster("data_statblocks_monsters_bugbear").IsValid);
        Assert.False(library.GetSpell("data_statblocks_spells_fireball").IsValid);
    }

    [Fact]
    public void An_empty_or_missing_root_serves_nothing_without_throwing()
    {
        var library = new FileSystemStatLibrary(tempRoot);

        Assert.False(library.GetMonster("data_statblocks_monsters_bugbear").IsValid);
        Assert.False(library.GetSpell("data_statblocks_spells_fireball").IsValid);
        Assert.False(library.HasMonster("data_statblocks_monsters_bugbear"));
    }

    [Fact]
    public void Every_monsters_and_spells_document_is_read()
    {
        // A published library sits beside a campaign's own documents, and neither is edited to add
        // to the other.
        Write("monsters-srd.json", Monster("bugbear", "Bugbear", armourClass: 16));
        Write("monsters.json", Monster("goblin", "Goblin", armourClass: 15));
        Write("spells-srd.json", Spell("fireball", "Fireball"));
        Write("spells.json", Spell("bless", "Bless"));

        var library = new FileSystemStatLibrary(tempRoot);

        Assert.Equal(2, library.AllMonsters().Count);
        Assert.True(library.HasMonster("data_statblocks_monsters_bugbear"));
        Assert.True(library.HasMonster("data_statblocks_monsters_goblin"));
        Assert.Equal(2, library.AllSpells().Count);
        Assert.True(library.GetSpell("data_statblocks_spells_fireball").IsValid);
        Assert.True(library.GetSpell("data_statblocks_spells_bless").IsValid);
    }

    [Fact]
    public void A_campaigns_own_block_replaces_the_librarys_under_the_same_node_id()
    {
        // '-' sorts before '.', so the library loads first and the campaign's own document wins.
        Write("monsters-srd.json", Monster("bugbear", "Bugbear", armourClass: 16));
        Write("monsters.json", Monster("bugbear", "Bugbear", armourClass: 17));

        var library = new FileSystemStatLibrary(tempRoot);

        Assert.Equal(17, library.GetMonster("data_statblocks_monsters_bugbear").Value.ArmourClass);
        Assert.Single(library.AllMonsters());
    }

    [Fact]
    public void Documents_load_in_ordinal_file_name_order()
    {
        // Ordinally 'Z' (0x5A) sorts before 'a' (0x61), so the alpha document loads last and wins.
        // NTFS lists a folder case-insensitively, alpha first, so without the ordinal sort this
        // machine would hand the win to Zeta.
        Write("monsters-alpha.json", Monster("bugbear", "Bugbear", armourClass: 18));
        Write("monsters-Zeta.json", Monster("bugbear", "Bugbear", armourClass: 19));

        var library = new FileSystemStatLibrary(tempRoot);

        Assert.Equal(18, library.GetMonster("data_statblocks_monsters_bugbear").Value.ArmourClass);
    }

    [Fact]
    public void A_corrupt_document_is_skipped_and_the_others_of_its_kind_still_load()
    {
        Write("monsters-srd.json", "[ not valid json ");
        Write("monsters.json", Monster("goblin", "Goblin", armourClass: 15));

        var library = new FileSystemStatLibrary(tempRoot);

        Assert.Equal("Goblin", Assert.Single(library.AllMonsters()).Name);
    }

    [Fact]
    public void A_null_entry_in_a_monsters_or_spells_document_is_skipped()
    {
        // The constructor read every entry's node id, so one hand-edited null threw out of it, and the
        // app stopped before it served a single request.
        Write("monsters.json", """[ null, { "nodeId": "data_statblocks_monsters_goblin", "name": "Goblin", "armourClass": 15 }, null ]""");
        Write("spells.json", """[ null, { "nodeId": "data_statblocks_spells_bless", "name": "Bless" } ]""");

        var library = new FileSystemStatLibrary(tempRoot);

        Assert.Equal("Goblin", Assert.Single(library.AllMonsters()).Name);
        Assert.Equal("Bless", Assert.Single(library.AllSpells()).Name);
    }

    [Fact]
    public void A_null_list_or_a_null_entry_inside_a_stat_block_is_read_leniently()
    {
        // The documents are hand-edited like the dossiers, so they are read the dossiers' way: a null
        // list is an empty one and a null entry is dropped, rather than reaching the stat block view.
        Write("monsters.json", """
            [
              {
                "nodeId": "data_statblocks_monsters_goblin",
                "name": "Goblin",
                "armourClass": 15,
                "senses": [ null, "darkvision 60 ft." ],
                "traits": null,
                "actions": [ null, { "name": "Scimitar", "text": "Melee Weapon Attack.", "attacks": [ null ] } ],
                "spellcasting": { "slots": null, "spells": [ null ] }
              }
            ]
            """);

        StatBlock goblin = new FileSystemStatLibrary(tempRoot).GetMonster("data_statblocks_monsters_goblin").Value;

        Assert.Equal("darkvision 60 ft.", Assert.Single(goblin.Senses));
        Assert.Empty(goblin.Traits);
        Assert.Empty(Assert.Single(goblin.Actions).Attacks);
        Assert.Empty(goblin.Spellcasting.Slots);
        Assert.Empty(goblin.Spellcasting.Spells);
    }

    private void Write(string fileName, string json)
    {
        Directory.CreateDirectory(tempRoot);
        File.WriteAllText(Path.Combine(tempRoot, fileName), json);
    }

    // The least a document needs to load: every other field has a default.
    private static string Monster(string slug, string name, int armourClass) =>
        $$"""[ { "nodeId": "data_statblocks_monsters_{{slug}}", "name": "{{name}}", "armourClass": {{armourClass}} } ]""";

    private static string Spell(string slug, string name) =>
        $$"""[ { "nodeId": "data_statblocks_spells_{{slug}}", "name": "{{name}}" } ]""";

    private void WriteMonsters()
    {
        Directory.CreateDirectory(tempRoot);
        const string json = """
        [
          {
            "nodeId": "data_statblocks_monsters_bugbear",
            "name": "Bugbear",
            "size": "Medium",
            "creatureType": "humanoid (goblinoid)",
            "alignment": "chaotic evil",
            "armourClass": 16,
            "armourNote": "hide armor, shield",
            "averageHitPoints": 27,
            "hitDice": "5d8 + 5",
            "speeds": [ { "kind": "walk", "feet": 30, "note": "" } ],
            "abilities": { "str": 15, "dex": 14, "con": 13, "intelligence": 8, "wis": 11, "cha": 9 },
            "savingThrows": [],
            "skills": [ { "skill": "Stealth", "bonus": 6 } ],
            "damageVulnerabilities": [],
            "damageResistances": [],
            "damageImmunities": [],
            "conditionImmunities": [],
            "senses": [ "darkvision 60 ft." ],
            "passivePerception": 10,
            "languages": [ "Common", "Goblin" ],
            "challengeRating": "1",
            "xp": 200,
            "proficiencyBonus": 2,
            "traits": [],
            "actions": [
              {
                "name": "Morningstar",
                "text": "Melee Weapon Attack: +4 to hit, reach 5 ft., one target. Hit: 11 (2d8 + 2) piercing damage.",
                "attacks": [
                  {
                    "name": "Morningstar", "kind": "meleeWeapon", "toHit": 4, "reach": 5, "rangeNormal": 0,
                    "rangeLong": 0, "targets": 1, "damageDice": "2d8 + 2", "damageAverage": 11,
                    "damageType": "piercing", "extraDamage": [], "onHit": ""
                  }
                ]
              }
            ],
            "bonusActions": [],
            "reactions": [],
            "legendaryActions": [],
            "legendaryActionsPerRound": 0,
            "spellcasting": {
              "ability": "", "saveDc": 0, "attackBonus": 0, "casterLevel": 0,
              "slots": [], "spells": [], "note": ""
            },
            "source": "SRD_CC_v5.1.pdf",
            "sourceLocation": "p.266"
          }
        ]
        """;
        File.WriteAllText(Path.Combine(tempRoot, "monsters.json"), json);
    }

    private void WriteSpells()
    {
        Directory.CreateDirectory(tempRoot);
        const string json = """
        [
          {
            "nodeId": "data_statblocks_spells_fireball",
            "name": "Fireball",
            "level": 3,
            "school": "evocation",
            "castingTime": "1 action",
            "range": "150 feet",
            "components": "V, S, M (a tiny ball of bat guano and sulfur)",
            "duration": "Instantaneous",
            "concentration": false,
            "ritual": false,
            "text": "A bright streak flashes from your pointing finger...",
            "higherLevels": "When you cast this spell using a spell slot of 4th level or higher...",
            "source": "SRD_CC_v5.1.pdf",
            "sourceLocation": "p.144"
          }
        ]
        """;
        File.WriteAllText(Path.Combine(tempRoot, "spells.json"), json);
    }
}
