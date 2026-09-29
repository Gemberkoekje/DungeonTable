using DungeonTable.Application.Abstractions;
using DungeonTable.Core.Dossier;

namespace DungeonTable.Infrastructure.Content;

/// <summary>
/// The <see cref="ICrossRefResolver"/> the pages see: prose links resolved against the content showing
/// now (<see cref="LiveContent.Current"/>). The resolver's cache belongs to its snapshot, so a reload
/// starts with an empty one rather than serving links the old content made.
/// </summary>
public sealed class LiveCrossRefResolver : ICrossRefResolver
{
    private readonly LiveContent live;

    /// <summary>Creates the resolver over the live content.</summary>
    /// <param name="live">The live content.</param>
    public LiveCrossRefResolver(LiveContent live)
    {
        ArgumentNullException.ThrowIfNull(live);
        this.live = live;
    }

    /// <inheritdoc />
    public IReadOnlyList<CrossRef> Resolve(string text) => live.Current.Resolver.Resolve(text);

    /// <inheritdoc />
    public IReadOnlyList<CrossRef> Resolve(string text, string levelNodeId) => live.Current.Resolver.Resolve(text, levelNodeId);
}
