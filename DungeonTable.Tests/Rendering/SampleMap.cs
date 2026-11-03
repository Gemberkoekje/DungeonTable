using System.Collections.Generic;
using System.Threading;
using DungeonTable.Core.Maps;
using DungeonTable.Core.Maps.Vector;
using DungeonTable.Infrastructure.Maps;

namespace DungeonTable.Tests.Rendering;

/// <summary>
/// The sample pack's map, read once for the rendering tests: its drawing and the Room Editor's notes on
/// it, with the ids of the doors and objects the notes mark secret, double or concealed.
/// </summary>
internal static class SampleMap
{
    private static readonly Lazy<(VectorMap Map, MapDefinition Notes)> Loaded = new Lazy<(VectorMap, MapDefinition)>(() =>
    {
        var read = new DungeonScrawlMapReader().ReadFileAsync(SamplePack.MapFile, CancellationToken.None).GetAwaiter().GetResult();
        var notes = new FileSystemMapStore(SamplePack.Maps).LoadAsync(SamplePack.MapId, CancellationToken.None).GetAwaiter().GetResult();
        Assert.True(read.IsValid);
        Assert.True(notes.IsValid);
        return (read.Value, notes.Value);
    });

    /// <summary>The map's drawing.</summary>
    internal static VectorMap Map => Loaded.Value.Map;

    /// <summary>The Room Editor's notes on the map.</summary>
    internal static MapDefinition Notes => Loaded.Value.Notes;

    /// <summary>The doors the notes mark secret.</summary>
    internal static string[] SecretDoors => Ids(o => o.Kind == MapObjectKind.Door && o.IsSecret);

    /// <summary>The doors the notes mark as double doors.</summary>
    internal static string[] DoubleDoors => Ids(o => o.Kind == MapObjectKind.Door && o.DoubleDoors);

    /// <summary>The stairs and decorations the notes mark concealed.</summary>
    internal static string[] Concealed => Ids(o => o.Kind != MapObjectKind.Door && o.IsConcealed);

    /// <summary>The doors that are not secret.</summary>
    internal static string[] PlainDoors => Map.Doors.Select(d => d.Id).Except(SecretDoors).ToArray();

    private static string[] Ids(Func<ObjectAnnotation, bool> which) =>
        Notes.Objects.Where(which).Select(o => o.ObjectId).ToArray();
}
