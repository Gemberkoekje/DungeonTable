using DungeonTable.Core.Art;

namespace DungeonTable.Application.Abstractions;

/// <summary>
/// Which pictures show what: the art catalogue's pictures by the things they depict (a person, an
/// entity, a stat block, a book entry, an area), so wherever one of those is mentioned the DM can put
/// its picture on the projector with one click. Answered from the content and the catalogue showing
/// now, both of which can change while the app runs.
/// </summary>
/// <remarks>
/// A picture names what it shows the way a link names it (<see cref="ArtImage.Shows"/>), so the ids
/// asked about here are the ids a resolved link carries: a person's or an entity's id, a stat block's
/// or an area's node id, a book entry's id.
/// </remarks>
public interface IPictureLinks
{
    /// <summary>
    /// Raised after the answers may have changed: the content was read again, or the catalogue changed
    /// (an upload, a removal, the committed catalogue edited). Raised outside any lock, on the thread
    /// that made the change; a view that handles it marshals onto its own dispatcher.
    /// </summary>
    event Action Changed;

    /// <summary>
    /// The pictures that show a thing, best first: the one that names it earliest among what it shows,
    /// then the one that shows the fewest things (a portrait before a group sheet), then by title.
    /// </summary>
    /// <param name="targetId">The id a resolved link carries ("wenna-brask", "data_statblocks_monsters_gargoyle").</param>
    /// <returns>The pictures (possibly empty).</returns>
    IReadOnlyList<ArtImage> PicturesOf(string targetId);
}
