namespace DungeonTable.ContentTests;

/// <summary>
/// The content root itself: it holds a campaign, and every kind of content the app needs to start is
/// where the app looks for it. Without the first, every other rule would pass with nothing to check.
/// </summary>
public sealed class ContentRootTests
{
    [Fact]
    public void The_content_root_holds_a_campaign()
    {
        Assert.True(
            ContentRoot.Dossiers.AllLevels().Count > 0 && ContentRoot.Dossiers.AllAreas().Count > 0,
            $"{ContentRoot.Described} has no level file with an area in it, so the other rules would check nothing. "
            + $"Point {ContentRoot.Variable} at a content root: the folder that holds Maps/ and data/.");
    }

    [Theory]
    [InlineData("Maps")]
    [InlineData("data/dossiers")]
    [InlineData("data/statblocks")]
    [InlineData("data/art")]
    [InlineData("data")]
    public void Every_kind_of_content_the_app_needs_to_start_is_where_it_looks(string folder)
    {
        Exception missing = Record.Exception(() => Locate(folder));

        Assert.True(missing is null, $"The app would not start on {ContentRoot.Described}: {missing?.Message}");
    }

    private static string Locate(string folder) => folder switch
    {
        "Maps" => ContentRoot.MapsFolder,
        "data/dossiers" => ContentRoot.DossiersFolder,
        "data/statblocks" => ContentRoot.StatsFolder,
        "data/art" => ContentRoot.ArtFolder,
        _ => ContentRoot.RostersFolder,
    };
}
