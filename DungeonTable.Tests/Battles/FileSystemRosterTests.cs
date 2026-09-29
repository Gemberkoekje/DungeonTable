using System.IO;
using DungeonTable.Core.Battle;
using DungeonTable.Infrastructure.Rosters;
using Qowaiv.Validation.Abstractions;

namespace DungeonTable.Tests.Battles;

/// <summary>
/// Verifies the two roster adapters round-trip their documents, treat a roster that has never been
/// saved as empty rather than broken, report a corrupt one instead of silently replacing it, and
/// leave no temporary files behind — the DM's own party is the one document here that would really
/// hurt to lose.
/// </summary>
public sealed class FileSystemRosterTests : IDisposable
{
    private readonly string tempRoot =
        Path.Combine(Path.GetTempPath(), "dt-rosters-" + Guid.NewGuid().ToString("N"));

    public FileSystemRosterTests() => Directory.CreateDirectory(tempRoot);

    public void Dispose()
    {
        if (Directory.Exists(tempRoot))
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void A_party_that_has_never_been_saved_reads_as_empty_not_as_an_error()
    {
        var roster = new FileSystemPartyRoster(tempRoot);

        Result<Party> party = roster.GetParty();

        Assert.True(party.IsValid);
        Assert.Empty(party.Value.Members);
    }

    [Fact]
    public void A_saved_party_round_trips_every_field()
    {
        var roster = new FileSystemPartyRoster(tempRoot);
        var saved = new Party
        {
            Members = new[]
            {
                new PartyMember
                {
                    Name = "Rurik",
                    PlayerName = "Sam",
                    Class = "Cleric",
                    Level = 5,
                    ArmourClass = 18,
                    MaxHp = 38,
                    PassivePerception = 14,
                    InitiativeModifier = 1,
                },
            },
        };

        Assert.True(roster.Save(saved).IsValid);

        PartyMember member = Assert.Single(new FileSystemPartyRoster(tempRoot).GetParty().Value.Members);
        Assert.Equal("Rurik", member.Name);
        Assert.Equal("Sam", member.PlayerName);
        Assert.Equal("Cleric", member.Class);
        Assert.Equal(5, member.Level);
        Assert.Equal(18, member.ArmourClass);
        Assert.Equal(38, member.MaxHp);
        Assert.Equal(14, member.PassivePerception);
        Assert.Equal(1, member.InitiativeModifier);
    }

    [Fact]
    public void The_document_is_written_in_camel_case()
    {
        var roster = new FileSystemPartyRoster(tempRoot);
        roster.Save(new Party { Members = new[] { new PartyMember { Name = "Rurik", MaxHp = 38 } } });

        string json = File.ReadAllText(Path.Combine(tempRoot, "party.json"));

        Assert.Contains("\"maxHp\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"MaxHp\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Saving_twice_replaces_rather_than_appends_and_leaves_no_temp_files()
    {
        var roster = new FileSystemPartyRoster(tempRoot);
        roster.Save(new Party { Members = new[] { new PartyMember { Name = "Rurik" } } });

        roster.Save(new Party { Members = new[] { new PartyMember { Name = "Sora" } } });

        Assert.Equal("Sora", Assert.Single(roster.GetParty().Value.Members).Name);
        Assert.Empty(Directory.GetFiles(tempRoot, "*.tmp"));
    }

    [Fact]
    public void A_corrupt_party_is_reported_rather_than_read_as_empty()
    {
        File.WriteAllText(Path.Combine(tempRoot, "party.json"), "{ not json");
        var roster = new FileSystemPartyRoster(tempRoot);

        Result<Party> party = roster.GetParty();

        Assert.False(party.IsValid);
        Assert.NotEmpty(party.Messages);
    }

    [Fact]
    public void An_ally_roster_round_trips_including_its_stat_block_link()
    {
        var roster = new FileSystemAllyRoster(tempRoot);
        roster.Save(new AllyRoster
        {
            Members = new[]
            {
                new AllyMember
                {
                    Name = "Shadow",
                    Owner = "Rurik's companion",
                    StatBlockNodeId = "data_statblocks_monsters_wolf",
                    ArmourClass = 13,
                    MaxHp = 11,
                    PassivePerception = 13,
                    InitiativeModifier = 2,
                },
            },
        });

        AllyMember member = Assert.Single(new FileSystemAllyRoster(tempRoot).GetAllies().Value.Members);
        Assert.Equal("Shadow", member.Name);
        Assert.Equal("Rurik's companion", member.Owner);
        Assert.Equal("data_statblocks_monsters_wolf", member.StatBlockNodeId);
        Assert.Equal(11, member.MaxHp);
    }

    [Fact]
    public void The_two_rosters_use_separate_documents()
    {
        new FileSystemPartyRoster(tempRoot).Save(new Party { Members = new[] { new PartyMember { Name = "Rurik" } } });
        new FileSystemAllyRoster(tempRoot).Save(new AllyRoster { Members = new[] { new AllyMember { Name = "Shadow" } } });

        Assert.Equal("Rurik", Assert.Single(new FileSystemPartyRoster(tempRoot).GetParty().Value.Members).Name);
        Assert.Equal("Shadow", Assert.Single(new FileSystemAllyRoster(tempRoot).GetAllies().Value.Members).Name);
    }

    [Fact]
    public void Saving_into_a_directory_that_does_not_exist_yet_creates_it()
    {
        string nested = Path.Combine(tempRoot, "fresh");
        var roster = new FileSystemPartyRoster(nested);

        Assert.True(roster.Save(new Party { Members = new[] { new PartyMember { Name = "Rurik" } } }).IsValid);
        Assert.Equal("Rurik", Assert.Single(roster.GetParty().Value.Members).Name);
    }

    [Fact]
    public void The_locator_finds_the_data_directory_by_walking_up()
    {
        string data = Path.Combine(tempRoot, "data");
        Directory.CreateDirectory(data);
        string deep = Path.Combine(tempRoot, "bin", "Debug", "net10.0");
        Directory.CreateDirectory(deep);

        Assert.Equal(data, RosterFileLocator.Locate(deep, configuredPath: string.Empty));
    }

    [Fact]
    public void The_locator_prefers_a_configured_directory_that_exists()
    {
        string configured = Path.Combine(tempRoot, "elsewhere");
        Directory.CreateDirectory(configured);
        Directory.CreateDirectory(Path.Combine(tempRoot, "data"));

        Assert.Equal(configured, RosterFileLocator.Locate(tempRoot, configured));
    }
}
