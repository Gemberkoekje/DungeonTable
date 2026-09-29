using System.Collections.Generic;
using DungeonTable.ContentTests.Rules;
using DungeonTable.Core.Dossier;
using DungeonTable.Infrastructure.Links;

namespace DungeonTable.ContentTests.RuleTests;

/// <summary>
/// Proves the quest rules catch a quest that cannot be shown or offered, a prerequisite or giver that
/// names nothing, a beat that cannot be ticked, and a destination that leads somewhere else.
/// </summary>
public sealed class QuestAuditTests
{
    private const string Workshop = "data_dossiers_level_1_area_4";

    private static readonly HashSet<string> NoNpcs = new(StringComparer.Ordinal);

    [Fact]
    public void A_quest_that_can_be_shown_and_is_offered_as_it_says_is_not_reported()
    {
        Assert.Empty(QuestAudit.Unshowable(Log(Quest(), Quest("the-silent-bell", QuestAvailability.Future, new[] { Prerequisite() }), Quest("the-lost-hymnal", QuestAvailability.Side))));
    }

    [Theory]
    [InlineData("id", "has no id, so the app drops it")]
    [InlineData("title", "has no title")]
    [InlineData("hook", "has no hook")]
    [InlineData("beats", "has no beats")]
    [InlineData("availability", "says nothing of when it is offered")]
    [InlineData("starting with a prerequisite", "is offered from the start, but has prerequisites")]
    [InlineData("future without one", "is offered once its prerequisites are met, but has none")]
    public void A_quest_that_cannot_be_shown_or_contradicts_its_offer_is_reported(string slip, string expected)
    {
        Quest quest = slip switch
        {
            "id" => new Quest { Title = "The Miller's Son", Hook = "Tam is missing.", Availability = QuestAvailability.Starting, Beats = new[] { Beat("hear-wenna-out", 1) } },
            "title" => new Quest { Id = "q", Hook = "Tam is missing.", Availability = QuestAvailability.Starting, Beats = new[] { Beat("hear-wenna-out", 1) } },
            "hook" => new Quest { Id = "q", Title = "The Miller's Son", Availability = QuestAvailability.Starting, Beats = new[] { Beat("hear-wenna-out", 1) } },
            "beats" => new Quest { Id = "q", Title = "The Miller's Son", Hook = "Tam is missing.", Availability = QuestAvailability.Starting },
            "availability" => Quest("q", QuestAvailability.None),
            "starting with a prerequisite" => Quest("q", QuestAvailability.Starting, new[] { Prerequisite() }),
            _ => Quest("q", QuestAvailability.Future),
        };

        string fault = Assert.Single(QuestAudit.Unshowable(Log(quest)));

        Assert.Contains(expected, fault, StringComparison.Ordinal);
    }

    [Fact]
    public void A_quest_id_written_twice_in_a_document_or_in_two_is_reported()
    {
        QuestLog main = Log(Quest(), Quest(), Quest("the-lost-hymnal", QuestAvailability.Side));
        QuestLog side = Log(Quest("the-lost-hymnal", QuestAvailability.Side));

        IReadOnlyList<string> faults = QuestAudit.WrittenTwice("quests.json", main, new[] { ("quests.json", main), ("quests-side.json", side) });

        Assert.Equal(2, faults.Count);
        Assert.Contains("'the-millers-son' is written more than once in quests.json", faults[0], StringComparison.Ordinal);
        Assert.Contains("'the-lost-hymnal' is also in quests-side.json", faults[1], StringComparison.Ordinal);
    }

    [Fact]
    public void A_prerequisite_that_can_be_judged_is_not_reported()
    {
        QuestLog log = Log(Quest("the-silent-bell", QuestAvailability.Future, new[] { Prerequisite(), new QuestPrerequisite { Kind = QuestPrerequisiteKind.CharacterLevel, CharacterLevel = 3, Text = "3rd level." } }));

        Assert.Empty(QuestAudit.BadPrerequisites(log, Ids("the-millers-son")));
    }

    [Theory]
    [InlineData("kind", "says nothing of what it waits for")]
    [InlineData("text", "has no text to show")]
    [InlineData("no quest", "names no quest to complete")]
    [InlineData("unknown quest", "waits for the quest 'the-millers-sun', which no quest document has")]
    [InlineData("level 0", "waits for character level 0")]
    [InlineData("level 21", "waits for character level 21")]
    public void A_prerequisite_that_cannot_be_judged_is_reported(string slip, string expected)
    {
        QuestPrerequisite prerequisite = slip switch
        {
            "kind" => new QuestPrerequisite { Text = "Something." },
            "text" => new QuestPrerequisite { Kind = QuestPrerequisiteKind.QuestComplete, AnyOfQuestIds = new[] { "the-millers-son" } },
            "no quest" => new QuestPrerequisite { Kind = QuestPrerequisiteKind.QuestComplete, Text = "Something." },
            "unknown quest" => new QuestPrerequisite { Kind = QuestPrerequisiteKind.QuestComplete, AnyOfQuestIds = new[] { "the-millers-sun" }, Text = "Something." },
            "level 0" => new QuestPrerequisite { Kind = QuestPrerequisiteKind.CharacterLevel, Text = "Something." },
            _ => new QuestPrerequisite { Kind = QuestPrerequisiteKind.CharacterLevel, CharacterLevel = 21, Text = "Something." },
        };

        string fault = Assert.Single(QuestAudit.BadPrerequisites(Log(Quest("the-silent-bell", QuestAvailability.Future, new[] { prerequisite })), Ids("the-millers-son")));

        Assert.Contains(expected, fault, StringComparison.Ordinal);
    }

    [Fact]
    public void Beats_with_ids_and_summaries_numbered_from_one_are_not_reported()
    {
        Assert.Empty(QuestAudit.BadBeats(Log(Quest())));
    }

    [Fact]
    public void A_beat_with_no_id_or_summary_a_repeated_id_or_the_wrong_numbering_is_reported()
    {
        var quest = new Quest
        {
            Id = "the-millers-son",
            Beats = new[]
            {
                Beat(string.Empty, 1),
                new QuestBeat { Id = "find-tam", Ordinal = 2 },
                Beat("find-tam", 4),
            },
        };

        IReadOnlyList<string> faults = QuestAudit.BadBeats(Log(quest));

        Assert.Equal(4, faults.Count);
        Assert.Contains("beats[0], has no id", faults[0], StringComparison.Ordinal);
        Assert.Contains("beat 'find-tam', has no summary", faults[1], StringComparison.Ordinal);
        Assert.Contains("two beats with the id 'find-tam'", faults[2], StringComparison.Ordinal);
        Assert.Contains("numbers its beats 1, 2, 4", faults[3], StringComparison.Ordinal);
    }

    [Fact]
    public void A_giver_that_opens_a_card_or_is_only_a_label_is_not_reported()
    {
        QuestLog log = Log(Quest(giverId: "wenna-brask", giverLabel: "Wenna Brask"), Quest("the-lost-hymnal", QuestAvailability.Side, giverLabel: "Old Hobb, the sexton"));

        Assert.Empty(QuestAudit.BadGivers(log, id => id == "wenna-brask", Ids("wenna-brask")));
    }

    [Fact]
    public void A_giver_that_opens_no_card_is_reported()
    {
        QuestLog log = Log(Quest(giverId: "wena-brask", giverLabel: "The miller"));

        string fault = Assert.Single(QuestAudit.BadGivers(log, id => id == "wenna-brask", NoNpcs));

        Assert.Contains("given by 'wena-brask', which opens no card", fault, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("the-old-mill")]
    public void A_giver_labelled_as_a_roster_npc_who_does_not_open_their_card_is_reported(string giverId)
    {
        QuestLog log = Log(Quest(giverId: giverId, giverLabel: "Wenna Brask"));

        string fault = Assert.Single(QuestAudit.BadGivers(log, _ => true, Ids("wenna-brask")));

        Assert.Contains("who is the NPC 'wenna-brask'", fault, StringComparison.Ordinal);
    }

    [Fact]
    public void A_destination_that_leads_where_it_says_is_not_reported()
    {
        QuestLog log = Log(Quest(targets: new[]
        {
            new QuestTarget { Kind = QuestTargetKind.Area, Level = 1, AreaKey = "4", NodeId = Workshop },
            new QuestTarget { Kind = QuestTargetKind.Area, Level = 2, AreaKey = "1", NodeId = "data_dossiers_level_2_area_1" },
            new QuestTarget { Kind = QuestTargetKind.Surface, PlaceLabel = "The mill" },
            new QuestTarget { Kind = QuestTargetKind.Place, Level = 2, PlaceLabel = "The ossuary" },
        }));

        Assert.Empty(QuestAudit.BadTargets(log, Targets()));
    }

    [Theory]
    [InlineData(1, "4", "data_dossiers_level_1_area_5", "that area is 'data_dossiers_level_1_area_4'")]
    [InlineData(1, "9", "data_dossiers_level_1_area_9", "has no area 9")]
    [InlineData(2, "1", "crypt-door", "Write the id its area 1 will have, 'data_dossiers_level_2_area_1'")]
    [InlineData(0, "1", "data_dossiers_level_0_area_1", "needs a level, an areaKey and the area's nodeId")]
    [InlineData(2, "", "data_dossiers_level_2_area_1", "needs a level, an areaKey and the area's nodeId")]
    [InlineData(2, "1", "", "needs a level, an areaKey and the area's nodeId")]
    public void An_area_destination_that_leads_elsewhere_or_nowhere_is_reported(int level, string key, string nodeId, string expected)
    {
        QuestLog log = Log(Quest(targets: new[] { new QuestTarget { Kind = QuestTargetKind.Area, Level = level, AreaKey = key, NodeId = nodeId } }));

        string fault = Assert.Single(QuestAudit.BadTargets(log, Targets()));

        Assert.Contains(expected, fault, StringComparison.Ordinal);
    }

    [Fact]
    public void A_destination_that_is_no_area_but_carries_an_id_is_reported()
    {
        QuestLog log = Log(Quest(targets: new[] { new QuestTarget { Kind = QuestTargetKind.Level, Level = 1, NodeId = "data_dossiers_level_1" } }));

        string fault = Assert.Single(QuestAudit.BadTargets(log, Targets()));

        Assert.Contains("only an area target jumps", fault, StringComparison.Ordinal);
    }

    private static QuestLog Log(params Quest[] quests) => new() { Quests = quests };

    // A quest that can be shown, with two beats, or with one beat per target when targets are given.
    private static Quest Quest(
        string id = "the-millers-son",
        QuestAvailability availability = QuestAvailability.Starting,
        IReadOnlyList<QuestPrerequisite> prerequisites = null,
        string giverId = "",
        string giverLabel = "Wenna Brask",
        IReadOnlyList<QuestTarget> targets = null) => new()
    {
        Id = id,
        Title = "The Miller's Son",
        Hook = "Tam is missing.",
        Availability = availability,
        GiverNodeId = giverId,
        GiverLabel = giverLabel,
        Prerequisites = prerequisites ?? Array.Empty<QuestPrerequisite>(),
        Beats = targets is null
            ? new[] { Beat("hear-wenna-out", 1), Beat("find-tam", 2) }
            : targets.Select((target, i) => new QuestBeat { Id = $"beat-{i}", Ordinal = i + 1, Summary = "Go.", Target = target }).ToArray(),
    };

    private static QuestBeat Beat(string id, int ordinal) => new() { Id = id, Ordinal = ordinal, Summary = "Hear Wenna out." };

    private static QuestPrerequisite Prerequisite() => new()
    {
        Kind = QuestPrerequisiteKind.QuestComplete,
        AnyOfQuestIds = new[] { "the-millers-son" },
        Text = "Complete The Miller's Son.",
    };

    private static HashSet<string> Ids(params string[] ids) => new(ids, StringComparer.Ordinal);

    // Level 1 is written, with its workshop as area 4; level 2 is not written yet.
    private static LinkTargets Targets()
    {
        var dossiers = new FakeDossierStore();
        var level = new LevelDossier { LevelNodeId = "data_dossiers_level_1" };
        dossiers.Areas[Workshop] = new AreaDossier { AreaNodeId = Workshop, Title = "Bell-Founder's Workshop (Level 1, Area 4)" };
        dossiers.LevelsByArea[Workshop] = level;
        dossiers.LevelNumbers["data_dossiers_level_1"] = 1;
        return LinkTargets.Build(dossiers, new FakeStatLibrary());
    }
}
