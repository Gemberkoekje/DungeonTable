using DungeonTable.ContentTests.Rules;
using DungeonTable.Core.Dossier;

namespace DungeonTable.ContentTests;

/// <summary>
/// The DM's prep: every session's scenes say who is on and run in order, and every story chapter has its
/// cast, each saying where they stand.
/// </summary>
public sealed class PrepTests
{
    public static TheoryData<string> SessionLogs => ContentDocuments.Names(DocumentKind.Sessions);

    public static TheoryData<string> Stories => ContentDocuments.Names(DocumentKind.Story);

    [Theory(SkipTestWithoutData = true)]
    [MemberData(nameof(SessionLogs))]
    public void Every_session_says_who_is_on_in_each_scene(string document)
    {
        SessionLog log = ContentDocuments.Loaded<SessionLog>(document);

        Report.None(PrepAudit.IncompleteSessions(log), $"Sessions in {document}");
    }

    [Theory(SkipTestWithoutData = true)]
    [MemberData(nameof(SessionLogs))]
    public void Every_sessions_scenes_run_in_order(string document)
    {
        SessionLog log = ContentDocuments.Loaded<SessionLog>(document);

        Report.None(PrepAudit.MisorderedScenes(log), $"Scene order in {document}");
    }

    [Theory(SkipTestWithoutData = true)]
    [MemberData(nameof(Stories))]
    public void Every_story_chapter_has_its_cast(string document)
    {
        StoryLibrary story = ContentDocuments.Loaded<StoryLibrary>(document);

        Report.None(PrepAudit.IncompleteChapters(story), $"Chapters in {document}");
    }
}
