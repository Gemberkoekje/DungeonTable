using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DungeonTable.Infrastructure.Art;
using DungeonTable.Infrastructure.Content;
using DungeonTable.Infrastructure.Maps;
using DungeonTable.Infrastructure.Rosters;
using DungeonTable.Web.Services;
using Microsoft.Extensions.Logging;

namespace DungeonTable.Tests.Web;

/// <summary>
/// The stores skip a document they cannot read and drop an entry they cannot use, without a word, so the
/// table stays up. The app now says so in its log as it starts, naming each file and why, for whoever
/// runs it without the SDK the content tests need.
/// </summary>
public sealed class ContentReportTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "dt-report-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task The_sample_pack_reads_with_no_problems_and_the_log_says_what_it_holds()
    {
        var logger = new RecordingLogger<ContentReport>();

        await Report(SamplePack.Root, logger).WriteAsync(CancellationToken.None);

        Assert.Empty(logger.At(LogLevel.Warning));
        string summary = Assert.Single(logger.At(LogLevel.Information));
        Assert.Contains($"Content read from {SamplePack.Root}.", summary, StringComparison.Ordinal);
        Assert.Contains("floors: 1, quests: 3, NPCs: 3, stat blocks: 317, spells: 319, pictures: 2, maps: 1.", summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Everything_skipped_or_dropped_is_named_with_its_file_and_why()
    {
        Write("data/dossiers/level-1.json", """
            { "level": { "levelNodeId": "floor-1" },
              "areas": [ { "areaNodeId": "floor-1-area-1", "title": "Landing" }, { "title": "The Nameless Room" }, null ] }
            """);
        Write("data/dossiers/npcs.json", """{ "npcs": [ { "id": "wenna-brask" """);
        Write("data/dossiers/npc.json", "{}");
        Write("data/dossiers/Level-2.json", "{}");
        Write("data/dossiers/quests.json", """{ "quests": [ { "title": "Untitled" }, { "id": "find-tam" }, { "id": "find-tam" } ] }""");
        Write("data/dossiers/deck-omens.json", """{ "title": "Omens" }""");
        Write("data/statblocks/monsters.json", """[ { "name": "Grask" } ]""");
        Write("data/statblocks/spells.json", """[ { "nodeId": "sacred-flame", "level": "high" } ]""");
        Write("data/book-index/almanac.json", "not json");
        Write("data/art/catalogue.json", """{ "images": [ { "title": "The Mill", "file": "demo/mill.webp" } ] }""");
        Write("data/party.json", """{ "members": [ { "name": "Maren", """);
        Write("data/allies.json", """{ "members": [] }""");
        Write("Maps/demo/cellar.regions.json", "{ \"regions\": 4 }");
        Write("Maps/demo/cellar.ds", "not a map");
        var logger = new RecordingLogger<ContentReport>();

        await Report(root, logger).WriteAsync(CancellationToken.None);

        IReadOnlyList<string> warnings = logger.At(LogLevel.Warning);
        AssertNamed(warnings, "data/dossiers/level-1.json: areas[1] ('The Nameless Room') has no areaNodeId, so the app dropped it.");
        AssertNamed(warnings, "data/dossiers/level-1.json: areas[2] is null, so the app drops it.");
        AssertNamed(warnings, "data/dossiers/npcs.json: it could not be read, so the app skipped it: ");
        AssertNamed(warnings, "data/dossiers/npc.json: the app reads no file by this name, so it is skipped. The documents in its folder are named level-*.json, reference.json");
        AssertNamed(warnings, "data/dossiers/Level-2.json: the app reads no file by this name, so it is skipped. Names are matched exactly, casing and all, on Linux, so write it in lower case.");
        AssertNamed(warnings, "data/dossiers/quests.json: quests[0] ('Untitled') has no id, so the app dropped it.");
        AssertNamed(warnings, "data/dossiers/quests.json: the quest 'find-tam' is also written in quests.json; the app keeps this one, the later.");
        AssertNamed(warnings, "data/dossiers/deck-omens.json: the deck ('Omens') has no id, so the app skipped it");
        AssertNamed(warnings, "data/statblocks/monsters.json: [0] ('Grask') has no nodeId, so the app dropped it.");
        AssertNamed(warnings, "data/statblocks/spells.json: it could not be read, so the app skipped it: ");
        AssertNamed(warnings, "data/book-index/almanac.json: it could not be read, so the app skipped it: ");
        AssertNamed(warnings, "data/art/catalogue.json: images[0] ('The Mill') has no id or no file, so the app dropped it.");
        AssertNamed(warnings, "cellar.regions.json: Map 'cellar' is not valid JSON");
        AssertNamed(warnings, "cellar.ds: ");
        AssertNamed(warnings, "party.json: The party roster at ");
        Assert.Equal($"{warnings.Count - 1} content problems, listed above. The app skipped or dropped what each one names, so a tab may look short until it is fixed. The content tests check a campaign in full.", warnings[^1]);
    }

    [Fact]
    public async Task One_problem_is_counted_as_one()
    {
        Write("data/dossiers/quests.json", """{ "quests": [ { "title": "Untitled" } ] }""");
        var logger = new RecordingLogger<ContentReport>();

        await Report(root, logger).WriteAsync(CancellationToken.None);

        IReadOnlyList<string> warnings = logger.At(LogLevel.Warning);
        Assert.Equal(2, warnings.Count);
        Assert.Equal("1 content problem, listed above. The app skipped or dropped what it names, so a tab may look short until it is fixed. The content tests check a campaign in full.", warnings[^1]);
    }

    [Fact]
    public async Task A_map_counts_as_soon_as_it_is_drawn_before_its_rooms_are_marked()
    {
        // A first map has no .regions.json until the Room Editor saves one, and the summary must not
        // say that no map was found.
        string drawn = Path.Combine(root, "Maps", "demo", "undercroft.ds");
        Directory.CreateDirectory(Path.GetDirectoryName(drawn));
        File.Copy(Path.Combine(SamplePack.Root, "Maps", "demo", "level-1-undercroft.ds"), drawn);
        Write("data/dossiers/campaign.json", "{}");
        var logger = new RecordingLogger<ContentReport>();

        await Report(root, logger).WriteAsync(CancellationToken.None);

        Assert.Contains("maps: 1.", Assert.Single(logger.At(LogLevel.Information)), StringComparison.Ordinal);
    }

    // One warning names the file and starts with what happened.
    private static void AssertNamed(IReadOnlyList<string> warnings, string fileAndProblem) =>
        Assert.Contains(warnings, warning => warning.StartsWith($"Content problem in {fileAndProblem}", StringComparison.Ordinal));

    private static ContentReport Report(string contentRoot, ILogger<ContentReport> logger)
    {
        string maps = Path.Combine(contentRoot, "Maps");
        string data = Path.Combine(contentRoot, "data");
        return new ContentReport(
            new LiveContent(Path.Combine(data, "dossiers"), Path.Combine(data, "statblocks"), Path.Combine(data, "book-index")),
            new FileSystemArtCatalogue(Path.Combine(data, "art")),
            new FileSystemMapStore(maps),
            new FileSystemVectorMapStore(maps, new DungeonScrawlMapReader()),
            new FileSystemPartyRoster(data),
            new FileSystemAllyRoster(data),
            contentRoot,
            logger);
    }

    private void Write(string relative, string content)
    {
        string path = Path.Combine(root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, content);
    }
}
