using System.Threading;
using DungeonTable.Application.Abstractions;
using DungeonTable.Core.Art;
using DungeonTable.Infrastructure.Links;

namespace DungeonTable.Infrastructure.Content;

/// <summary>
/// The <see cref="IPictureLinks"/> the pages see: the catalogue's pictures by what they show, resolved
/// against the content showing now (<see cref="LiveContent.Current"/>). The index is built when it is
/// first asked for after the content was read again or the catalogue changed, and kept until the next
/// change, so a page asking about every link it draws costs a dictionary lookup each.
/// </summary>
/// <remarks>
/// Two things change it, apart: the content (a new snapshot, whose link targets the pictures' names
/// resolve against) and the catalogue (an upload, a removal, the committed catalogue read again). The
/// catalogue lives outside the snapshot, so the index remembers which reading of each it was built from.
/// </remarks>
public sealed class LivePictureLinks : IPictureLinks
{
    private readonly LiveContent live;
    private readonly IArtCatalogue art;
    private Built built;
    private int catalogueRevision;

    /// <summary>Creates the index over the live content and the catalogue, and follows both.</summary>
    /// <param name="live">The live content.</param>
    /// <param name="art">The art catalogue.</param>
    public LivePictureLinks(LiveContent live, IArtCatalogue art)
    {
        ArgumentNullException.ThrowIfNull(live);
        ArgumentNullException.ThrowIfNull(art);
        this.live = live;
        this.art = art;

        // Both are singletons that live as long as this one, so neither subscription is ever dropped.
        live.Changed += OnContentChanged;
        art.Changed += OnCatalogueChanged;
    }

    /// <inheritdoc />
    public event Action Changed;

    /// <inheritdoc />
    public IReadOnlyList<ArtImage> PicturesOf(string targetId) => Now().Index.PicturesOf(targetId);

    // The index for the content and the catalogue as they are now. The revision is read before the
    // catalogue, so a change that lands while this builds leaves a stale revision behind and the next
    // call builds again, rather than the other way round.
    private Built Now()
    {
        Built current = Volatile.Read(ref built);
        ContentSnapshot content = live.Current;
        int revision = Volatile.Read(ref catalogueRevision);
        if (current is not null && ReferenceEquals(current.Content, content) && current.Revision == revision)
        {
            return current;
        }

        current = new Built(content, revision, PictureIndex.Build(art.All(), content.Targets));
        Volatile.Write(ref built, current);
        return current;
    }

    private void OnContentChanged(ContentChanges changes)
    {
        if (changes.HasFlag(ContentChanges.Documents))
        {
            Changed?.Invoke();
        }
    }

    private void OnCatalogueChanged()
    {
        Interlocked.Increment(ref catalogueRevision);
        Changed?.Invoke();
    }

    // One index, with the readings it was built from.
    private sealed record Built(ContentSnapshot Content, int Revision, PictureIndex Index);
}
