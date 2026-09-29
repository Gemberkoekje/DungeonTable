using DungeonTable.Core.Dossier;
using DungeonTable.Web.Services;

namespace DungeonTable.Tests.Web;

/// <summary>The names a card and a tooltip give each kind of reference.</summary>
public sealed class CrossRefStyleTests
{
    [Fact]
    public void An_item_is_not_called_magic()
    {
        // Level 1's stone key and green copper helm are items, and neither is magic.
        Assert.Equal("Item", CrossRefStyle.KindName(CrossRefKind.Item));
    }
}
