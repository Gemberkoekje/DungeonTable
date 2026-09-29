using System;
using DungeonTable.Web.Services;
using Microsoft.AspNetCore.Components;

namespace DungeonTable.Web.Components.Dm;

/// <summary>
/// Base for the DM screen's tab panels. Injects the two pieces of state every tab reads — the
/// per-circuit <see cref="DmWorkspace"/> and the shared <see cref="SessionState"/> — and re-renders
/// the tab whenever either changes.
/// </summary>
/// <remarks>
/// The tabs must observe that state themselves rather than rely on the shell re-rendering them:
/// Blazor skips a child component whose parameters are unchanged, and these panels take few
/// parameters (the Battle tab takes none), so a shell re-render would leave them showing whatever
/// they rendered when the circuit started.
/// </remarks>
public abstract class DmTabBase : ComponentBase, IDisposable
{
    /// <summary>The per-circuit DM state: the loaded map, its annotations, the shown briefing.</summary>
    [Inject]
    protected DmWorkspace Workspace { get; set; }

    /// <summary>The shared DM/player state: the player viewport and everything revealed so far.</summary>
    [Inject]
    protected SessionState Session { get; set; }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        Workspace.Changed += OnStateChanged;
        Session.Changed += OnStateChanged;
    }

    /// <summary>Unsubscribes from the shared state, which outlives this component.</summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Unsubscribes from the shared state.</summary>
    /// <param name="disposing">True when called from <see cref="Dispose()"/>.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            Workspace.Changed -= OnStateChanged;
            Session.Changed -= OnStateChanged;
        }
    }

    /// <summary>
    /// Called just before the tab re-renders because the shared state changed. Override it to pull
    /// local editable state (a bound text box, say) back in step with the workspace — a tab that
    /// re-renders itself never gets <see cref="ComponentBase.OnParametersSet"/>, which is where that
    /// sync would otherwise live.
    /// </summary>
    protected virtual void OnSharedStateChanged()
    {
    }

    private void OnStateChanged() => InvokeAsync(() =>
    {
        OnSharedStateChanged();
        StateHasChanged();
    });
}
