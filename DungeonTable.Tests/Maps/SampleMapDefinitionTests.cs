using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DungeonTable.Core.Briefing;
using DungeonTable.Core.Maps;
using DungeonTable.Core.Maps.Vector;
using DungeonTable.Infrastructure.Maps;

namespace DungeonTable.Tests.Maps;

/// <summary>
/// The sample pack's Room Editor annotations, loaded the way the host loads them. The pack was
/// annotated to use each kind of annotation once: a region per room, a double, an open and a secret
/// door, two concealed placements, and a trap and a hazard marker.
/// </summary>
public sealed class SampleMapDefinitionTests
{
    // Every area with floor of its own. Area 3 is an overview that 3a and 3b fill, so it has no region
    // of its own, and the passage between the landing and the den belongs to no area.
    private static readonly string[] RegionedAreas =
    {
        "data_dossiers_level_1_area_1",
        "data_dossiers_level_1_area_2",
        "data_dossiers_level_1_area_3a",
        "data_dossiers_level_1_area_3b",
        "data_dossiers_level_1_area_4",
        "data_dossiers_level_1_area_5",
    };

    private static readonly string[] ConcealedPlacements = { "chest", "pit" };

    [Fact]
    public void The_maps_root_lists_the_sample_map()
    {
        Assert.Equal(SamplePack.MapId, Assert.Single(Store().ListMapIds()));
    }

    [Fact]
    public async Task The_sample_map_loads_under_its_level_name()
    {
        MapDefinition map = await Load();

        Assert.Equal(SamplePack.MapId, map.MapId);
        Assert.Equal(SamplePack.Adventure, map.Adventure);
        Assert.Equal("Level 1 - The Undercroft", map.LevelName);
    }

    [Fact]
    public async Task A_region_opens_each_area_that_has_floor_of_its_own()
    {
        MapDefinition map = await Load();

        Assert.Equal(RegionedAreas, map.Regions.Select(region => region.GraphNodeId).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task One_door_is_double_one_starts_open_and_one_is_secret()
    {
        MapDefinition map = await Load();
        ObjectAnnotation[] doors = map.Objects.Where(annotation => annotation.Kind == MapObjectKind.Door).ToArray();

        Assert.Single(doors, door => door.DoubleDoors);
        Assert.Single(doors, door => door.DefaultOpen);
        Assert.Single(doors, door => door.IsSecret);
    }

    [Fact]
    public async Task The_chest_and_the_pit_are_concealed()
    {
        // Shown on the projector, the pit's sprite would tell the players where the trap is.
        MapDefinition map = await Load();
        VectorMap drawing = (await new DungeonScrawlMapReader().ReadFileAsync(SamplePack.MapFile, CancellationToken.None)).Value;
        Dictionary<string, string> names = drawing.Decorations.ToDictionary(decoration => decoration.Id, decoration => decoration.Name, StringComparer.Ordinal);

        IEnumerable<string> concealed = map.Objects
            .Where(annotation => annotation.IsConcealed)
            .Select(annotation => names[annotation.ObjectId])
            .Order(StringComparer.Ordinal);

        Assert.Equal(ConcealedPlacements, concealed);
    }

    [Fact]
    public async Task A_trap_and_a_hazard_are_marked()
    {
        MapDefinition map = await Load();

        Assert.Single(map.Features, feature => feature.Kind == FeatureKind.Trap);
        Assert.Single(map.Features, feature => feature.Kind == FeatureKind.Hazard);
    }

    private static FileSystemMapStore Store() => new FileSystemMapStore(SamplePack.Maps);

    private static async Task<MapDefinition> Load()
    {
        var result = await Store().LoadAsync(SamplePack.MapId, CancellationToken.None);
        Assert.True(result.IsValid, string.Join("; ", result.Messages.Select(message => message.Message)));
        return result.Value;
    }
}
