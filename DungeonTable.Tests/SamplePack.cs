using System.IO;
using Microsoft.AspNetCore.Hosting;

namespace DungeonTable.Tests;

/// <summary>
/// The sample content pack, <c>samples/demo</c>: what the tests run the app against instead of the
/// campaign's own content, so they pass in a checkout that holds nothing else.
/// </summary>
internal static class SamplePack
{
    /// <summary>The pack's map: <c>Maps/demo/level-1-undercroft.ds</c> and its <c>.regions.json</c>.</summary>
    internal const string MapId = "level-1-undercroft";

    /// <summary>The pack's adventure, the folder its map sits in.</summary>
    internal const string Adventure = "demo";

    /// <summary>The pack's folder, found by walking up from the test output.</summary>
    internal static string Root { get; } = Find();

    /// <summary>The pack's maps root.</summary>
    internal static string Maps => Path.Combine(Root, "Maps");

    /// <summary>The pack's Dungeon Scrawl map.</summary>
    internal static string MapFile => Path.Combine(Maps, Adventure, MapId + ".ds");

    /// <summary>
    /// Points a test host at the pack and nothing else. <c>Content:Root</c> is the pack, and ASP.NET's
    /// own content root is an empty folder, so a kind of content the pack lacked would stop the host
    /// starting instead of being found by walking up into the campaign's folder.
    /// </summary>
    /// <param name="builder">The test host's builder.</param>
    internal static void Serve(IWebHostBuilder builder)
    {
        builder.UseContentRoot(EmptyFolder());
        builder.UseSetting("Content:Root", Root);
    }

    private static string Find()
    {
        for (DirectoryInfo dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            string pack = Path.Combine(dir.FullName, "samples", "demo");
            if (Directory.Exists(Path.Combine(pack, "Maps")))
            {
                return pack;
            }
        }

        throw new DirectoryNotFoundException("Could not find the sample pack (samples/demo) above the test output directory.");
    }

    // One empty folder outside the repository, shared by every host. The app writes nothing into its
    // own content root, so it stays empty.
    private static string EmptyFolder()
    {
        string folder = Path.Combine(Path.GetTempPath(), "dungeontable-tests-empty-root");
        Directory.CreateDirectory(folder);
        return folder;
    }
}
