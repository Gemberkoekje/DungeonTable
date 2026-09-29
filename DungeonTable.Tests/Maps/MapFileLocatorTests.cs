using System.IO;
using DungeonTable.Infrastructure.Maps;

namespace DungeonTable.Tests.Maps;

/// <summary>Verifies the maps-root locator walks up the tree and honours a configured path.</summary>
public sealed class MapFileLocatorTests
{
    [Fact]
    public void Locates_the_maps_directory_walking_up()
    {
        // A Maps folder with a map in it, three levels above where the walk starts.
        string tempRoot = Path.Combine(Path.GetTempPath(), "dt-walk-" + Guid.NewGuid().ToString("N"));
        string maps = Path.Combine(tempRoot, "Maps");
        string start = Path.Combine(tempRoot, "app", "bin", "net10.0");
        Directory.CreateDirectory(maps);
        Directory.CreateDirectory(start);
        File.WriteAllText(Path.Combine(maps, "level.regions.json"), "{}");

        try
        {
            Assert.Equal(maps, MapFileLocator.Locate(start, configuredPath: null));
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Uses_a_configured_path_when_it_exists()
    {
        string configured = AppContext.BaseDirectory;

        string root = MapFileLocator.Locate(AppContext.BaseDirectory, configured);

        Assert.Equal(Path.GetFullPath(configured), Path.GetFullPath(root));
    }

    [Fact]
    public void Skips_a_same_named_directory_without_map_assets()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "dt-skip-" + Guid.NewGuid().ToString("N"));
        string bareMaps = Path.Combine(tempRoot, "Maps");
        string start = Path.Combine(tempRoot, "deep", "start");
        Directory.CreateDirectory(bareMaps);
        Directory.CreateDirectory(start);
        File.WriteAllText(Path.Combine(bareMaps, "notes.txt"), "not a map");

        try
        {
            Assert.Throws<DirectoryNotFoundException>(
                () => MapFileLocator.Locate(start, configuredPath: null));
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Throws_when_no_maps_directory_is_found()
    {
        string isolated = Path.Combine(Path.GetTempPath(), "dt-noroot-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(isolated);

        try
        {
            Assert.Throws<DirectoryNotFoundException>(
                () => MapFileLocator.Locate(isolated, configuredPath: null));
        }
        finally
        {
            Directory.Delete(isolated, recursive: true);
        }
    }
}
