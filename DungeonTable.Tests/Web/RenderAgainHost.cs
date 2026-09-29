using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace DungeonTable.Tests.Web;

/// <summary>
/// Renders its content, and renders it again on request. Blazor compares <c>@key</c>s only when it
/// diffs a list against the one it rendered before, so a duplicate key passes a first render and
/// takes the page down on the next one; a test that renders once cannot see it.
/// </summary>
internal sealed class RenderAgainHost : ComponentBase
{
    /// <summary>What to render.</summary>
    [Parameter]
    public RenderFragment ChildContent { get; set; }

    /// <summary>Handed this host once it exists, so the test can ask for the second render.</summary>
    [Parameter]
    public Action<RenderAgainHost> OnReady { get; set; }

    /// <summary>Renders the content again, diffing it against the first render.</summary>
    /// <returns>A task that completes once rendered.</returns>
    public Task RenderAgainAsync() => InvokeAsync(StateHasChanged);

    /// <inheritdoc />
    protected override void OnInitialized() => OnReady?.Invoke(this);

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder) => builder.AddContent(0, ChildContent);
}
