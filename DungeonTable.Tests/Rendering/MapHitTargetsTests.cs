using System.Collections.Generic;
using DungeonTable.Core.Maps;
using DungeonTable.Web.Rendering;

namespace DungeonTable.Tests.Rendering;

/// <summary>
/// The click targets over the DM's map, on the sample pack: a plain click in Look opens and closes
/// doors, but never reaches a secret door the players have not found, so one is never discovered by
/// accident.
/// </summary>
public sealed class MapHitTargetsTests
{
    [Fact]
    public void The_sample_pack_has_both_kinds_of_door()
    {
        // The tests below mean nothing on a map without a secret door and a plain one.
        Assert.NotEmpty(SampleMap.SecretDoors);
        Assert.NotEmpty(SampleMap.PlainDoors);
    }

    [Fact]
    public void Look_leaves_an_undiscovered_secret_door_to_the_room_beneath()
    {
        string[] targets = Ids(MapHitTargets.LookDoors(SampleMap.Map, SampleMap.SecretDoors, Array.Empty<string>()));

        Assert.Equal(SampleMap.PlainDoors.OrderBy(id => id, StringComparer.Ordinal), targets.OrderBy(id => id, StringComparer.Ordinal));
        Assert.DoesNotContain(targets, SampleMap.SecretDoors.Contains);
    }

    [Fact]
    public void Look_opens_a_secret_door_once_it_is_found()
    {
        string found = SampleMap.SecretDoors[0];

        string[] targets = Ids(MapHitTargets.LookDoors(SampleMap.Map, SampleMap.SecretDoors, new[] { found }));

        Assert.Contains(found, targets);
        Assert.Equal(SampleMap.PlainDoors.Length + 1, targets.Length);
    }

    [Fact]
    public void The_doors_tool_reaches_every_door_secret_or_not()
    {
        string[] targets = Ids(MapHitTargets.Doors(SampleMap.Map));

        Assert.Equal(SampleMap.Map.Doors.Count, targets.Length);
        Assert.All(SampleMap.SecretDoors, id => Assert.Contains(id, targets));
    }

    [Fact]
    public void Conceal_reaches_only_what_the_room_editor_marked_concealed()
    {
        Assert.NotEmpty(SampleMap.Concealed);

        var targets = MapHitTargets.Concealed(SampleMap.Map, SampleMap.Concealed);

        Assert.Equal(SampleMap.Concealed.OrderBy(id => id, StringComparer.Ordinal), Ids(targets).OrderBy(id => id, StringComparer.Ordinal));
        Assert.DoesNotContain(targets, target => target.Kind == MapObjectKind.Door);
    }

    private static string[] Ids(IEnumerable<ObjectHighlight> targets) => targets.Select(target => target.Id).ToArray();
}
