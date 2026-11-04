using DungeonTable.Web.Services;

namespace DungeonTable.Tests.Web;

/// <summary>
/// The map's tools: Look by default, the one-click tools giving way to it once they have done
/// something, the tools used in a run staying until Esc, and Shift keeping a one-click tool up.
/// </summary>
public sealed class ToolSelectionTests
{
    [Fact]
    public void The_map_starts_in_look()
    {
        var tools = new ToolSelection();

        Assert.Equal(MapTool.Look, tools.Active);
        Assert.False(tools.Kept);
    }

    [Theory]
    [InlineData(MapTool.Reveal)]
    [InlineData(MapTool.Doors)]
    [InlineData(MapTool.Conceal)]
    [InlineData(MapTool.Ping)]
    [InlineData(MapTool.Party)]
    public void A_one_click_tool_gives_way_to_look_once_it_has_been_used(MapTool tool)
    {
        var tools = new ToolSelection();
        tools.Pick(tool, keep: false);

        tools.Used();

        Assert.Equal(MapTool.Look, tools.Active);
    }

    [Theory]
    [InlineData(MapTool.Fog)]
    [InlineData(MapTool.Measure)]
    public void A_tool_used_in_a_run_stays_after_use(MapTool tool)
    {
        var tools = new ToolSelection();
        tools.Pick(tool, keep: false);

        tools.Used();
        tools.Used();

        Assert.Equal(tool, tools.Active);
    }

    [Fact]
    public void Shift_keeps_a_one_click_tool_up_until_esc()
    {
        var tools = new ToolSelection();
        tools.Pick(MapTool.Reveal, keep: true);

        tools.Used();
        tools.Used();

        Assert.Equal(MapTool.Reveal, tools.Active);
        Assert.True(tools.Kept);

        tools.Reset();

        Assert.Equal(MapTool.Look, tools.Active);
        Assert.False(tools.Kept);
    }

    [Fact]
    public void Shift_means_nothing_to_a_tool_that_stays_anyway()
    {
        var tools = new ToolSelection();

        tools.Pick(MapTool.Fog, keep: true);

        Assert.False(tools.Kept);
    }

    [Fact]
    public void Picking_the_tool_that_is_up_puts_look_back()
    {
        var tools = new ToolSelection();
        tools.Pick(MapTool.Measure, keep: false);

        tools.Pick(MapTool.Measure, keep: false);

        Assert.Equal(MapTool.Look, tools.Active);
    }

    [Fact]
    public void Picking_with_shift_the_tool_that_is_up_keeps_it_instead()
    {
        var tools = new ToolSelection();
        tools.Pick(MapTool.Doors, keep: false);

        tools.Pick(MapTool.Doors, keep: true);
        tools.Used();

        Assert.Equal(MapTool.Doors, tools.Active);
    }

    [Fact]
    public void Picking_look_while_in_look_stays_in_look()
    {
        var tools = new ToolSelection();

        tools.Pick(MapTool.Look, keep: false);

        Assert.Equal(MapTool.Look, tools.Active);
    }

    [Fact]
    public void Esc_after_a_kept_tool_does_not_leave_the_next_pick_kept()
    {
        var tools = new ToolSelection();
        tools.Pick(MapTool.Ping, keep: true);
        tools.Reset();

        tools.Pick(MapTool.Ping, keep: false);
        tools.Used();

        Assert.Equal(MapTool.Look, tools.Active);
    }
}
