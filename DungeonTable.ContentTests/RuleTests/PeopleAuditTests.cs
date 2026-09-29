using System.Collections.Generic;
using DungeonTable.ContentTests.Rules;
using DungeonTable.Core.Battle;
using DungeonTable.Core.Dossier;
using DungeonTable.Core.Stats;

namespace DungeonTable.ContentTests.RuleTests;

/// <summary>Proves the people rules catch a card missing what it shows, and a party whose two documents disagree.</summary>
public sealed class PeopleAuditTests
{
    private static readonly DossierBlock Block = new() { Heading = "Tam went down at dusk", Body = "With the mill's lantern." };

    [Fact]
    public void An_npc_with_everything_their_card_shows_is_not_reported()
    {
        Assert.Empty(PeopleAudit.IncompleteNpcs(new NpcRoster { Npcs = new[] { Wenna() } }));
    }

    [Theory]
    [InlineData("id", "has no id")]
    [InlineData("name", "has no name")]
    [InlineData("role", "has no role")]
    [InlineData("where", "says nowhere to find them")]
    [InlineData("disposition", "has no disposition")]
    [InlineData("summary", "has no summary")]
    [InlineData("knows", "knows nothing")]
    [InlineData("hooks", "has no hooks")]
    public void An_npc_missing_something_their_card_shows_is_reported(string missing, string expected)
    {
        NpcDossier npc = missing switch
        {
            "id" => Wenna(id: string.Empty),
            "name" => Wenna(name: string.Empty),
            "role" => Wenna(role: " "),
            "where" => Wenna(where: string.Empty),
            "disposition" => Wenna(disposition: string.Empty),
            "summary" => Wenna(summary: string.Empty),
            "knows" => Wenna(knows: false),
            _ => Wenna(hooks: false),
        };

        string fault = Assert.Single(PeopleAudit.IncompleteNpcs(new NpcRoster { Npcs = new[] { npc } }));

        Assert.Contains(expected, fault, StringComparison.Ordinal);
    }

    [Fact]
    public void Two_npcs_with_one_id_are_reported()
    {
        string fault = Assert.Single(PeopleAudit.IncompleteNpcs(new NpcRoster { Npcs = new[] { Wenna(), Wenna() } }));

        Assert.Contains("Two NPCs have the id 'wenna-brask'", fault, StringComparison.Ordinal);
    }

    [Fact]
    public void A_contact_or_cast_row_that_does_not_say_where_they_stand_is_reported()
    {
        NpcDossier wenna = Wenna(contacts: new[]
        {
            new CastMember { Name = "Tam Brask", Role = "Her son", State = "Missing" },
            new CastMember { Name = "Brother Aldous", Role = "The acolyte" },
            new CastMember { Role = "Someone", State = "Somewhere" },
        });

        IReadOnlyList<string> faults = PeopleAudit.IncompleteContacts(new NpcRoster { Npcs = new[] { wenna } });

        Assert.Equal(2, faults.Count);
        Assert.Contains("npcs.json 'wenna-brask'.contacts[1] (Brother Aldous) does not say where they stand", faults[0], StringComparison.Ordinal);
        Assert.Contains("contacts[2] has no name", faults[1], StringComparison.Ordinal);
    }

    [Fact]
    public void A_character_with_everything_their_card_shows_is_not_reported()
    {
        Assert.Empty(PeopleAudit.IncompleteCharacters(new PartyDossier { Characters = new[] { Maren() } }));
    }

    [Theory]
    [InlineData("level 0", "is level 0")]
    [InlineData("level 21", "is level 21")]
    [InlineData("ac", "has no AC")]
    [InlineData("hp", "has no hit points")]
    [InlineData("perception", "has no passive Perception")]
    [InlineData("speed", "has no speed")]
    [InlineData("player", "has no player")]
    [InlineData("build", "has no build line")]
    [InlineData("backstory", "has no backstory")]
    [InlineData("abilities", "has no abilities")]
    [InlineData("hooks", "has no hooks")]
    public void A_character_missing_something_their_card_shows_is_reported(string missing, string expected)
    {
        CharacterDossier maren = missing switch
        {
            "level 0" => Maren(level: 0),
            "level 21" => Maren(level: 21),
            "ac" => Maren(ac: 0),
            "hp" => Maren(hp: 0),
            "perception" => Maren(perception: 0),
            "speed" => Maren(speed: string.Empty),
            "player" => Maren(player: string.Empty),
            "build" => Maren(build: string.Empty),
            "backstory" => Maren(backstory: string.Empty),
            "abilities" => Maren(abilities: false),
            _ => Maren(hooks: false),
        };

        string fault = Assert.Single(PeopleAudit.IncompleteCharacters(new PartyDossier { Characters = new[] { maren } }));

        Assert.Contains(expected, fault, StringComparison.Ordinal);
    }

    [Fact]
    public void A_roster_that_agrees_with_the_dossier_or_is_empty_is_not_reported()
    {
        var party = new PartyDossier { Characters = new[] { Maren() } };

        Assert.Empty(PeopleAudit.Disagreements(party, new Party { Members = new[] { Member() } }));
        Assert.Empty(PeopleAudit.Disagreements(party, new Party()));
    }

    [Fact]
    public void A_roster_that_disagrees_with_the_dossier_is_reported_number_by_number()
    {
        var party = new PartyDossier { Characters = new[] { Maren() } };
        PartyMember member = Member();
        member.Level = 3;
        member.ArmourClass = 16;
        member.MaxHp = 20;
        member.PassivePerception = 12;
        member.InitiativeModifier = 1;
        member.PlayerName = "Rob";

        IReadOnlyList<string> faults = PeopleAudit.Disagreements(party, new Party { Members = new[] { member } });

        Assert.Equal(6, faults.Count);
        Assert.Contains("Maren's level is 2 in characters.json and 3 in party.json", faults[0], StringComparison.Ordinal);
        Assert.Contains("Maren's player is 'Robin' in characters.json and 'Rob' in party.json", faults[5], StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void A_character_missing_from_the_roster_or_in_it_twice_is_reported(int times)
    {
        var party = new PartyDossier { Characters = new[] { Maren() } };
        var others = new PartyMember { Name = "Hild", PlayerName = "Sam", Level = 2 };
        PartyMember[] members = Enumerable.Repeat(0, times).Select(_ => Member()).Append(others).ToArray();

        string fault = Assert.Single(PeopleAudit.Disagreements(party, new Party { Members = members }));

        Assert.Contains($"Maren is in party.json {times} times", fault, StringComparison.Ordinal);
    }

    [Fact]
    public void An_ally_whose_stat_block_the_library_lacks_is_reported()
    {
        var stats = new FakeStatLibrary();
        stats.Monsters["goblin"] = new StatBlock { NodeId = "goblin", Name = "Goblin" };
        var allies = new AllyRoster
        {
            Members = new[]
            {
                new AllyMember { Name = "Scrap", StatBlockNodeId = "goblin" },
                new AllyMember { Name = "The mule" },
                new AllyMember { Name = "Bramble", StatBlockNodeId = "gobln" },
            },
        };

        string fault = Assert.Single(PeopleAudit.BadAllyBlocks(allies, stats));

        Assert.Contains("Bramble's statBlockNodeId 'gobln'", fault, StringComparison.Ordinal);
    }

    private static NpcDossier Wenna(
        string id = "wenna-brask",
        string name = "Wenna Brask",
        string role = "The miller",
        string where = "The mill",
        string disposition = "Ally",
        string summary = "Runs the mill.",
        bool knows = true,
        bool hooks = true,
        IReadOnlyList<CastMember> contacts = null) => new()
    {
        Id = id,
        Name = name,
        Role = role,
        Where = where,
        Disposition = disposition,
        Summary = summary,
        Knows = knows ? new[] { Block } : Array.Empty<DossierBlock>(),
        Hooks = hooks ? new[] { Block } : Array.Empty<DossierBlock>(),
        Contacts = contacts ?? Array.Empty<CastMember>(),
    };

    private static CharacterDossier Maren(
        int level = 2,
        int ac = 18,
        int hp = 17,
        int perception = 13,
        string speed = "30 ft.",
        string player = "Robin",
        string build = "Human Cleric 2",
        string backstory = "Wenna's niece.",
        bool abilities = true,
        bool hooks = true) => new()
    {
        Id = "maren",
        Name = "Maren",
        PlayerName = player,
        Build = build,
        Level = level,
        ArmourClass = ac,
        MaxHp = hp,
        PassivePerception = perception,
        Speed = speed,
        Backstory = backstory,
        Abilities = abilities ? new[] { Block } : Array.Empty<DossierBlock>(),
        Hooks = hooks ? new[] { Block } : Array.Empty<DossierBlock>(),
    };

    private static PartyMember Member() => new()
    {
        Name = "Maren",
        PlayerName = "Robin",
        Level = 2,
        ArmourClass = 18,
        MaxHp = 17,
        PassivePerception = 13,
        InitiativeModifier = 0,
    };
}
