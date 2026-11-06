using System;
using System.Threading.Tasks;
using DungeonTable.Web.Services;
using Microsoft.AspNetCore.Components;

namespace DungeonTable.Web.Components.Dm;

/// <summary>
/// Base for the DM screen's map pane and dock tabs. Injects the two pieces of state each reads — the
/// per-circuit <see cref="DmWorkspace"/> and the shared <see cref="SessionState"/> — and re-renders
/// the tab whenever either changes, together with the rest of the DM screen.
/// </summary>
/// <remarks>
/// <para>
/// Blazor skips a child component whose parameters are unchanged, and these panels take few parameters
/// (the Battle tab takes none), so a shell re-render alone would leave them showing whatever they
/// rendered when the circuit started. The shell (<see cref="DmView"/>) therefore cascades a revision
/// it bumps on every change of the shared state, under <see cref="RevisionName"/>: a new value
/// re-renders every tab that takes it, in the same render as the shell.
/// </para>
/// <para>
/// Each tab used to listen to the state itself and render on its own, so one change made one render
/// per tab, each a message to the browser and an acknowledgement back. On a phone's hotspot that was
/// most of the round trips a step of the party token cost.
/// </para>
/// <para>
/// A dock tab the DM is not looking at does not follow the table while hidden: the shell keeps all four
/// mounted and hides three, and a render of a hidden tab sends its changes all the same, every button's
/// handler among them (on a large campaign, three quarters of the screen's handlers sit in the Campaign
/// and Art tabs), on every step of the party token. The shell cascades which tab is up under
/// <see cref="ActiveTabName"/>, and a tab names the panel it lives in with <see cref="Panel"/>. A
/// hidden tab still renders when the workspace changes (a map loaded, a room shown, the content read
/// again: <see cref="WorkspaceRevisionName"/>), which is rare, and catches up on the rest when it is
/// shown, since the new active tab re-renders it. What is always on screen (the map, the pins, the
/// sheets) has no panel.
/// </para>
/// </remarks>
public abstract class DmTabBase : ComponentBase, IDisposable
{
    /// <summary>The name of the revision the DM shell cascades to its tabs.</summary>
    public const string RevisionName = "DmRevision";

    /// <summary>The name under which the DM shell cascades the dock tab that is up.</summary>
    public const string ActiveTabName = "DmActiveTab";

    /// <summary>The name of the revision the DM shell bumps when the workspace (not only the table) changes.</summary>
    public const string WorkspaceRevisionName = "DmWorkspaceRevision";

    private bool seen;
    private int seenRevision;
    private int renderedWorkspaceRevision;

    /// <summary>The per-circuit DM state: the loaded map, its annotations, the shown briefing.</summary>
    [Inject]
    protected DmWorkspace Workspace { get; set; }

    /// <summary>The shared DM/player state: the player viewport and everything revealed so far.</summary>
    [Inject]
    protected SessionState Session { get; set; }

    /// <summary>Bumped by the DM shell whenever the workspace or the shared state changes.</summary>
    [CascadingParameter(Name = RevisionName)]
    private int Revision { get; set; }

    /// <summary>Bumped by the DM shell whenever the workspace changes.</summary>
    [CascadingParameter(Name = WorkspaceRevisionName)]
    private int WorkspaceRevision { get; set; }

    /// <summary>The dock tab that is up; <see cref="DockTab.None"/> outside the DM shell.</summary>
    [CascadingParameter(Name = ActiveTabName)]
    private DockTab ActiveTab { get; set; }

    /// <summary>
    /// The dock panel this component lives in, which renders only while that tab is up; <see cref="DockTab.None"/>
    /// (the default) for what is always on screen.
    /// </summary>
    protected virtual DockTab Panel => DockTab.None;

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        parameters.SetParameterProperties(this);
        if (seen && Revision != seenRevision)
        {
            OnSharedStateChanged();
        }

        if (!seen)
        {
            // The first render, which follows, always happens.
            renderedWorkspaceRevision = WorkspaceRevision;
        }

        seen = true;
        seenRevision = Revision;
        return base.SetParametersAsync(ParameterView.Empty);
    }

    /// <inheritdoc />
    protected override bool ShouldRender()
    {
        bool shown = Panel == DockTab.None || ActiveTab == DockTab.None || Panel == ActiveTab;
        if (!shown && WorkspaceRevision == renderedWorkspaceRevision)
        {
            return false;
        }

        renderedWorkspaceRevision = WorkspaceRevision;
        return true;
    }

    /// <summary>Releases what a tab subscribed to; see <see cref="Dispose(bool)"/>.</summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Unsubscribes a tab from whatever else it listens to (the fight, the campaign, the art). The base
    /// listens to nothing itself: the shell's revision reaches it as a parameter.
    /// </summary>
    /// <param name="disposing">True when called from <see cref="Dispose()"/>.</param>
    protected virtual void Dispose(bool disposing)
    {
        // Nothing of the base's own to release.
    }

    /// <summary>
    /// Called just before the tab re-renders because the shared state changed. Override it to pull
    /// local editable state (a bound text box, say) back in step with the workspace.
    /// </summary>
    protected virtual void OnSharedStateChanged()
    {
    }
}
