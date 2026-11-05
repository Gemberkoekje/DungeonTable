using System.Collections.Generic;
using System.Threading.Tasks;
using DungeonTable.Web.Components.Dm;
using DungeonTable.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DungeonTable.Tests.Web;

/// <summary>
/// The DM screen's tabs re-render with the shell, in its render, when it cascades a new revision
/// (<see cref="DmTabBase"/>), rather than each on its own: one change used to make a render, and a
/// message to the browser, per tab.
/// </summary>
public sealed class DmTabRevisionTests
{
    [Fact]
    public async Task A_new_revision_re_renders_a_tab_and_tells_it_the_state_changed()
    {
        RevisionProbe probe = await RenderTwice<RevisionProbe>(new Shell(1, 1, DockTab.Room), new Shell(2, 1, DockTab.Room));

        Assert.Equal(2, probe.Renders);
        Assert.Equal(1, probe.SharedChanges);
    }

    [Fact]
    public async Task The_same_revision_again_leaves_a_tab_alone()
    {
        // Without a new revision Blazor skips the tab, whose parameters did not change: the reason the
        // shell has to bump it on every change.
        RevisionProbe probe = await RenderTwice<RevisionProbe>(new Shell(1, 1, DockTab.Room), new Shell(1, 1, DockTab.Room));

        Assert.Equal(1, probe.Renders);
        Assert.Equal(0, probe.SharedChanges);
    }

    [Fact]
    public async Task A_hidden_dock_tab_keeps_up_with_the_state_but_does_not_render()
    {
        // The Battle tab while the Room tab is up, and the table changed (a reveal, a step of the token): a
        // render of it would go to the browser for nothing.
        BattlePanelProbe probe = await RenderTwice<BattlePanelProbe>(new Shell(1, 1, DockTab.Room), new Shell(2, 1, DockTab.Room));

        Assert.Equal(1, probe.Renders);
        Assert.Equal(1, probe.SharedChanges);
    }

    [Fact]
    public async Task A_hidden_dock_tab_renders_when_the_workspace_changes()
    {
        // A map loaded, a room shown or the content read again: rare, and a tab shown later must not
        // first show the old map.
        BattlePanelProbe probe = await RenderTwice<BattlePanelProbe>(new Shell(1, 1, DockTab.Room), new Shell(2, 2, DockTab.Room));

        Assert.Equal(2, probe.Renders);
    }

    [Fact]
    public async Task A_dock_tab_renders_when_it_is_shown()
    {
        BattlePanelProbe probe = await RenderTwice<BattlePanelProbe>(new Shell(1, 1, DockTab.Room), new Shell(1, 1, DockTab.Battle));

        Assert.Equal(2, probe.Renders);
    }

    private static async Task<TProbe> RenderTwice<TProbe>(Shell first, Shell second)
        where TProbe : DmTabBase, ICountsRenders
    {
        PartyHarness table = await PartyHarness.Started();
        await using ServiceProvider services = new ServiceCollection()
            .AddSingleton(table.Workspace)
            .AddSingleton(table.Session)
            .BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, NullLoggerFactory.Instance);

        Shell shell = first;
        TProbe probe = null;
        RenderAgainHost host = null;
        RenderFragment content = builder =>
        {
            builder.OpenComponent<CascadingValue<int>>(0);
            builder.AddComponentParameter(1, nameof(CascadingValue<int>.Name), DmTabBase.RevisionName);
            builder.AddComponentParameter(2, nameof(CascadingValue<int>.Value), shell.Revision);
            builder.AddComponentParameter(3, nameof(CascadingValue<int>.ChildContent), (RenderFragment)(workspace =>
            {
                workspace.OpenComponent<CascadingValue<int>>(0);
                workspace.AddComponentParameter(1, nameof(CascadingValue<int>.Name), DmTabBase.WorkspaceRevisionName);
                workspace.AddComponentParameter(2, nameof(CascadingValue<int>.Value), shell.WorkspaceRevision);
                workspace.AddComponentParameter(3, nameof(CascadingValue<int>.ChildContent), (RenderFragment)(tabs =>
                {
                    tabs.OpenComponent<CascadingValue<DockTab>>(0);
                    tabs.AddComponentParameter(1, nameof(CascadingValue<DockTab>.Name), DmTabBase.ActiveTabName);
                    tabs.AddComponentParameter(2, nameof(CascadingValue<DockTab>.Value), shell.ActiveTab);
                    tabs.AddComponentParameter(3, nameof(CascadingValue<DockTab>.ChildContent), (RenderFragment)(inner =>
                    {
                        inner.OpenComponent<TProbe>(0);
                        inner.AddComponentReferenceCapture(1, reference => probe = (TProbe)reference);
                        inner.CloseComponent();
                    }));
                    tabs.CloseComponent();
                }));
                workspace.CloseComponent();
            }));
            builder.CloseComponent();
        };

        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.RenderComponentAsync<RenderAgainHost>(ParameterView.FromDictionary(new Dictionary<string, object>
            {
                [nameof(RenderAgainHost.ChildContent)] = content,
                [nameof(RenderAgainHost.OnReady)] = (Action<RenderAgainHost>)(h => host = h),
            }));
            shell = second;
            await host.RenderAgainAsync();
        });

        return probe;
    }

    /// <summary>What a probe counts.</summary>
    internal interface ICountsRenders
    {
        /// <summary>How often it rendered.</summary>
        int Renders { get; }

        /// <summary>How often it was told the shared state changed.</summary>
        int SharedChanges { get; }
    }

    // What the shell cascades: the revision of any change, the revision of the workspace's, and the dock
    // tab that is up.
    private sealed record Shell(int Revision, int WorkspaceRevision, DockTab ActiveTab);

    // A tab that is always on screen, counting its renders and the shared-state changes it was told of.
    internal class RevisionProbe : DmTabBase, ICountsRenders
    {
        public int Renders { get; private set; }

        public int SharedChanges { get; private set; }

        protected override void OnSharedStateChanged() => SharedChanges++;

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            Renders++;
            builder.AddContent(0, Renders);
        }
    }

    // The same, living in the Battle tab's panel.
    internal sealed class BattlePanelProbe : RevisionProbe
    {
        protected override DockTab Panel => DockTab.Battle;
    }
}
