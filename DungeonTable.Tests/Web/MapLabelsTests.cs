using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DungeonTable.Core.Maps;
using DungeonTable.Web.Services;

namespace DungeonTable.Tests.Web;

/// <summary>
/// A map picker calls each map by the level name set for it in the Room Editor, and by its id until
/// one is set.
/// </summary>
public sealed class MapLabelsTests
{
    private static readonly string[] Undercroft = { "level-1-undercroft" };

    // "no-notes" has a .ds but no annotations at all: nothing to read a name from.
    private static readonly string[] UnnamedAndNoNotes = { "unnamed", "no-notes" };

    [Fact]
    public async Task A_map_is_listed_under_the_level_name_its_annotations_give_it()
    {
        var store = new FakeMapStore();
        store.Definitions["level-1-undercroft"] = new MapDefinition
        {
            MapId = "level-1-undercroft",
            LevelName = "  Level 1 - The Undercroft ",
        };

        IReadOnlyDictionary<string, string> names =
            await MapLabels.LoadAsync(store, Undercroft, CancellationToken.None);

        Assert.Equal("Level 1 - The Undercroft", MapLabels.For(names, "level-1-undercroft"));
    }

    [Fact]
    public async Task A_map_with_no_name_yet_is_listed_under_its_id()
    {
        var store = new FakeMapStore();
        store.Definitions["unnamed"] = new MapDefinition { MapId = "unnamed", LevelName = "   " };

        IReadOnlyDictionary<string, string> names =
            await MapLabels.LoadAsync(store, UnnamedAndNoNotes, CancellationToken.None);

        Assert.Empty(names);
        Assert.Equal("unnamed", MapLabels.For(names, "unnamed"));
        Assert.Equal("no-notes", MapLabels.For(names, "no-notes"));
    }

    [Fact]
    public void Each_map_keeps_its_own_name()
    {
        var names = new Dictionary<string, string>(System.StringComparer.Ordinal)
        {
            ["level-1-undercroft"] = "Level 1 - The Undercroft",
            ["level-2-crypt"] = "Level 2 - The Sealed Crypt",
        };

        Assert.Equal("Level 2 - The Sealed Crypt", MapLabels.For(names, "level-2-crypt"));
        Assert.Equal("level-3-bell-tower", MapLabels.For(names, "level-3-bell-tower"));
    }
}
