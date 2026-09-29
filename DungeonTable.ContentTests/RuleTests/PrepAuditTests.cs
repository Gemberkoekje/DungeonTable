using System.Collections.Generic;
using DungeonTable.ContentTests.Rules;
using DungeonTable.Core.Dossier;

namespace DungeonTable.ContentTests.RuleTests;

/// <summary>Proves the prep rules catch a scene that does not say who is on, scenes out of order, and a chapter with no cast.</summary>
public sealed class PrepAuditTests
{
    private static readonly DossierBlock Beat = new() { Heading = "No bell", Body = "Dusk comes and the bell does not ring." };

    [Fact]
    public void A_session_whose_scenes_say_who_is_on_is_not_reported_and_needs_no_source()
    {
        var log = new SessionLog { Sessions = new[] { Session(Scene("cold-open", 0), Scene("the-chapel", 1)) } };

        Assert.Empty(PrepAudit.IncompleteSessions(log));
        Assert.Empty(PrepAudit.MisorderedScenes(log));
    }

    [Fact]
    public void A_session_or_scene_missing_what_its_card_shows_is_reported()
    {
        var log = new SessionLog
        {
            Sessions = new[]
            {
                new SessionPlan { Id = "01-dusk", Title = "Session 1", Scenes = new[] { new SessionScene { Id = "cold-open", Title = "The mill" } } },
            },
        };

        IReadOnlyList<string> faults = PrepAudit.IncompleteSessions(log);

        Assert.Equal(4, faults.Count);
        Assert.Contains("The session '01-dusk' has no summary", faults[0], StringComparison.Ordinal);
        Assert.Contains("scene 'cold-open', does not say who is on", faults[1], StringComparison.Ordinal);
        Assert.Contains("gives no timing", faults[2], StringComparison.Ordinal);
        Assert.Contains("has no beats", faults[3], StringComparison.Ordinal);
    }

    [Fact]
    public void Two_sessions_with_one_id_are_reported()
    {
        var log = new SessionLog { Sessions = new[] { Session(Scene("a", 0)), Session(Scene("a", 0)) } };

        string fault = Assert.Single(PrepAudit.IncompleteSessions(log));

        Assert.Contains("Two sessions have the id '01-dusk'", fault, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    public void Scenes_not_numbered_from_0_in_order_are_reported(int first, int second)
    {
        var log = new SessionLog { Sessions = new[] { Session(Scene("cold-open", first), Scene("the-chapel", second)) } };

        string fault = Assert.Single(PrepAudit.MisorderedScenes(log));

        Assert.Contains($"numbers its scenes {first}, {second}", fault, StringComparison.Ordinal);
    }

    [Fact]
    public void Two_scenes_with_one_id_are_reported()
    {
        var log = new SessionLog { Sessions = new[] { Session(Scene("cold-open", 0), Scene("cold-open", 1)) } };

        string fault = Assert.Single(PrepAudit.MisorderedScenes(log));

        Assert.Contains("two scenes with the id 'cold-open'", fault, StringComparison.Ordinal);
    }

    [Fact]
    public void A_chapter_with_its_cast_and_sections_is_not_reported_and_needs_no_source()
    {
        Assert.Empty(PrepAudit.IncompleteChapters(new StoryLibrary { Chapters = new[] { Chapter() } }));
    }

    [Fact]
    public void A_chapter_with_no_sections_no_cast_or_a_cast_row_missing_its_state_is_reported()
    {
        var story = new StoryLibrary
        {
            Chapters = new[]
            {
                new StoryDossier { Title = "Why the bell fell silent", Summary = "The debt." },
                Chapter(new CastMember { Name = "Grask", Role = "The bugbear" }),
            },
        };

        IReadOnlyList<string> faults = PrepAudit.IncompleteChapters(story);

        Assert.Equal(3, faults.Count);
        Assert.Contains("has no sections", faults[0], StringComparison.Ordinal);
        Assert.Contains("has no cast", faults[1], StringComparison.Ordinal);
        Assert.Contains("cast[0], (Grask) does not say where they stand", faults[2], StringComparison.Ordinal);
    }

    private static SessionPlan Session(params SessionScene[] scenes) => new()
    {
        Id = "01-dusk",
        Title = "Session 1 - The Miller at Dusk",
        Summary = "Wenna asks for help.",
        Scenes = scenes,
    };

    private static SessionScene Scene(string id, int ordinal) => new()
    {
        Id = id,
        Ordinal = ordinal,
        Title = "The mill at dusk",
        When = "Dusk, 10 minutes",
        Cast = "Maren, Oswin, Hild",
        Beats = new[] { Beat },
    };

    private static StoryDossier Chapter(params CastMember[] cast) => new()
    {
        Title = "Why the bell fell silent",
        Summary = "How the bell came to be cast.",
        Sections = new[] { Beat },
        Cast = cast.Length > 0 ? cast : new[] { new CastMember { Name = "Grask", Role = "The bugbear", State = "In the workshop" } },
    };
}
