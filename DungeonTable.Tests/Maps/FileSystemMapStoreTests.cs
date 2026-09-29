using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DungeonTable.Core.Briefing;
using DungeonTable.Core.Maps;
using DungeonTable.Infrastructure.Maps;

namespace DungeonTable.Tests.Maps;

/// <summary>
/// Verifies the file-system map store round-trips definitions and guards bad input,
/// using a throwaway temp directory as the maps root.
/// </summary>
public sealed class FileSystemMapStoreTests : IDisposable
{
    private readonly string tempRoot =
        Path.Combine(Path.GetTempPath(), "dt-maps-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(tempRoot))
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task Save_then_load_round_trips_a_definition()
    {
        var store = new FileSystemMapStore(tempRoot);
        MapDefinition map = SampleMap();

        var saved = await store.SaveAsync(map, CancellationToken.None);
        Assert.True(saved.IsValid);

        var loaded = await store.LoadAsync(map.MapId, CancellationToken.None);
        Assert.True(loaded.IsValid);

        MapDefinition result = loaded.Value;
        Assert.Equal(map.MapId, result.MapId);
        Assert.Equal(map.Adventure, result.Adventure);
        Assert.Equal(map.LevelName, result.LevelName);

        Region region = Assert.Single(result.Regions);
        Assert.Equal("r1", region.RegionId);
        Assert.Equal("Guard room", region.Label);
        Assert.Equal("data_dossiers_level_1_area_1", region.GraphNodeId);
        Assert.Equal(3, region.Polygon.Count);
        Assert.Equal(new MapPoint(3, 4), region.Polygon[1]);

        FeatureMarker feature = Assert.Single(result.Features);
        Assert.Equal(FeatureKind.Trap, feature.Kind);
        Assert.Equal("Pit trap", feature.Label);
        Assert.Equal(new MapPoint(7, 8), feature.Position);

        ObjectAnnotation annotation = Assert.Single(result.Objects);
        Assert.Equal("door-1", annotation.ObjectId);
        Assert.Equal(MapObjectKind.Door, annotation.Kind);
        Assert.True(annotation.IsSecret);
        Assert.Equal("data_dossiers_level_1_secret", annotation.GraphNodeId);
    }

    [Fact]
    public async Task Save_writes_under_the_adventure_subfolder()
    {
        var store = new FileSystemMapStore(tempRoot);

        var saved = await store.SaveAsync(SampleMap(), CancellationToken.None);

        Assert.True(saved.IsValid);
        Assert.True(File.Exists(Path.Combine(tempRoot, "test", "test-map.regions.json")));
    }

    [Fact]
    public async Task Saved_json_uses_camel_case_and_string_enums()
    {
        var store = new FileSystemMapStore(tempRoot);
        await store.SaveAsync(SampleMap(), CancellationToken.None);

        string json = await File.ReadAllTextAsync(
            Path.Combine(tempRoot, "test", "test-map.regions.json"), CancellationToken.None);

        Assert.Contains("\"mapId\"", json);
        Assert.Contains("\"kind\"", json);
        Assert.Contains("\"trap\"", json);
        Assert.DoesNotContain("\"MapId\"", json);
    }

    [Fact]
    public async Task Load_unknown_map_is_invalid_with_a_message()
    {
        var store = new FileSystemMapStore(tempRoot);

        var result = await store.LoadAsync("nope", CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Messages);
    }

    [Fact]
    public async Task Load_blank_id_is_invalid()
    {
        var store = new FileSystemMapStore(tempRoot);

        Assert.False((await store.LoadAsync("", CancellationToken.None)).IsValid);
        Assert.False((await store.LoadAsync("   ", CancellationToken.None)).IsValid);
    }

    [Fact]
    public async Task Load_corrupt_json_is_invalid_rather_than_throwing()
    {
        Directory.CreateDirectory(tempRoot);
        await File.WriteAllTextAsync(
            Path.Combine(tempRoot, "broken.regions.json"), "{ not valid json ", CancellationToken.None);
        var store = new FileSystemMapStore(tempRoot);

        var result = await store.LoadAsync("broken", CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Messages);
    }

    [Fact]
    public async Task Load_literal_null_json_is_invalid()
    {
        Directory.CreateDirectory(tempRoot);
        await File.WriteAllTextAsync(Path.Combine(tempRoot, "nothing.regions.json"), "null", CancellationToken.None);
        var store = new FileSystemMapStore(tempRoot);

        var result = await store.LoadAsync("nothing", CancellationToken.None);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Save_map_without_id_is_invalid()
    {
        var store = new FileSystemMapStore(tempRoot);

        var result = await store.SaveAsync(new MapDefinition(), CancellationToken.None);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void List_map_ids_on_empty_root_is_empty()
    {
        var store = new FileSystemMapStore(tempRoot);

        Assert.Empty(store.ListMapIds());
    }

    [Fact]
    public async Task List_map_ids_returns_saved_ids_sorted()
    {
        var store = new FileSystemMapStore(tempRoot);
        await store.SaveAsync(SampleMap("b-map"), CancellationToken.None);
        await store.SaveAsync(SampleMap("a-map"), CancellationToken.None);

        var ids = store.ListMapIds();
        Assert.Equal(2, ids.Count);
        Assert.Equal("a-map", ids[0]);
        Assert.Equal("b-map", ids[1]);
    }

    [Fact]
    public async Task Exists_reflects_whether_a_definition_is_present()
    {
        var store = new FileSystemMapStore(tempRoot);
        Assert.False(store.Exists("test-map"));

        await store.SaveAsync(SampleMap(), CancellationToken.None);

        Assert.True(store.Exists("test-map"));
        Assert.False(store.Exists("nope"));
        Assert.False(store.Exists(""));
    }

    [Fact]
    public async Task A_save_goes_through_a_temporary_file_and_leaves_none_behind()
    {
        // Written in place, a crash or a full disk mid-save could leave the map's notes truncated, and
        // with them every room, door and secret the Room Editor marked. Written beside it and moved
        // over it, the last save stays whole until the new one is complete.
        var store = new FileSystemMapStore(tempRoot);
        await store.SaveAsync(SampleMap(), CancellationToken.None);
        string folder = Path.Combine(tempRoot, "test");
        var temporaryFiles = new System.Collections.Concurrent.ConcurrentBag<string>();
        using var watcher = new FileSystemWatcher(folder, "*.tmp") { EnableRaisingEvents = true };
        watcher.Created += (_, created) => temporaryFiles.Add(created.Name);

        MapDefinition renamed = SampleMap();
        renamed = new MapDefinition { MapId = renamed.MapId, Adventure = renamed.Adventure, LevelName = "Renamed", Regions = renamed.Regions };
        Assert.True((await store.SaveAsync(renamed, CancellationToken.None)).IsValid);

        for (int waited = 0; temporaryFiles.IsEmpty && waited < 40; waited++)
        {
            await Task.Delay(50, CancellationToken.None);
        }

        Assert.NotEmpty(temporaryFiles);
        Assert.Empty(Directory.GetFiles(folder, "*.tmp"));
        Assert.Equal("Renamed", (await store.LoadAsync("test-map", CancellationToken.None)).Value.LevelName);
    }

    [Fact]
    public async Task A_save_that_cannot_replace_the_file_leaves_the_last_save_whole()
    {
        // An editor or a sync client holding the file open is a Windows case: Linux and macOS replace
        // an open file, so there the save succeeds, as it should.
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Only Windows refuses to replace a file another program has open.");
        var store = new FileSystemMapStore(tempRoot);
        await store.SaveAsync(SampleMap(), CancellationToken.None);
        string path = Path.Combine(tempRoot, "test", "test-map.regions.json");
        string before = await File.ReadAllTextAsync(path, CancellationToken.None);

        var result = await SaveWhileOpen(store, path);

        Assert.False(result.IsValid);
        Assert.Equal(before, await File.ReadAllTextAsync(path, CancellationToken.None));
        Assert.Empty(Directory.GetFiles(Path.Combine(tempRoot, "test"), "*.tmp"));
    }

    [Fact]
    public async Task A_save_whose_file_cannot_be_replaced_leaves_what_is_there_and_no_temporary_file()
    {
        // A folder where the map's notes go makes the final move fail on every platform, after the
        // temporary file is written.
        var store = new FileSystemMapStore(tempRoot);
        string path = Path.Combine(tempRoot, "test", "test-map.regions.json");
        Directory.CreateDirectory(path);
        await File.WriteAllTextAsync(Path.Combine(path, "kept.txt"), "kept", CancellationToken.None);

        var result = await store.SaveAsync(SampleMap(), CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Equal("kept", await File.ReadAllTextAsync(Path.Combine(path, "kept.txt"), CancellationToken.None));
        Assert.Empty(Directory.GetFiles(Path.Combine(tempRoot, "test"), "*.tmp"));
    }

    [Fact]
    public async Task A_save_that_cannot_make_its_folder_fails_with_a_message()
    {
        // A file where the adventure's folder goes: making the folder throws an IOException on every
        // platform, which the save reports rather than throws.
        var store = new FileSystemMapStore(tempRoot);
        Directory.CreateDirectory(tempRoot);
        await File.WriteAllTextAsync(Path.Combine(tempRoot, "test"), "not a folder", CancellationToken.None);

        var result = await store.SaveAsync(SampleMap(), CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Contains("could not be saved", Assert.Single(result.Messages).Message, StringComparison.Ordinal);
    }

    // Saves a changed map while the file is held open for reading only, so it cannot be replaced.
    private static async Task<Qowaiv.Validation.Abstractions.Result> SaveWhileOpen(FileSystemMapStore store, string path)
    {
        await using var held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        MapDefinition map = SampleMap();
        return await store.SaveAsync(new MapDefinition { MapId = map.MapId, Adventure = map.Adventure, LevelName = "Never saved" }, CancellationToken.None);
    }

    private static MapDefinition SampleMap(string mapId = "test-map", string adventure = "test") =>
        new MapDefinition
        {
            MapId = mapId,
            Adventure = adventure,
            LevelName = "Test Level",
            Regions = new[]
            {
                new Region
                {
                    RegionId = "r1",
                    Label = "Guard room",
                    GraphNodeId = "data_dossiers_level_1_area_1",
                    Polygon = new[] { new MapPoint(1, 2), new MapPoint(3, 4), new MapPoint(5, 6) },
                },
            },
            Features = new[]
            {
                new FeatureMarker
                {
                    FeatureId = "f1",
                    Kind = FeatureKind.Trap,
                    Label = "Pit trap",
                    Position = new MapPoint(7, 8),
                    GraphNodeId = "data_dossiers_level_1_trap",
                },
            },
            Objects = new[]
            {
                new ObjectAnnotation
                {
                    ObjectId = "door-1",
                    Kind = MapObjectKind.Door,
                    Label = "Secret door",
                    IsSecret = true,
                    GraphNodeId = "data_dossiers_level_1_secret",
                },
            },
        };
}
