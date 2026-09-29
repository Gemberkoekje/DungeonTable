using DungeonTable.Core.Art;
using Qowaiv.Validation.Abstractions;

namespace DungeonTable.Application.Abstractions;

/// <summary>
/// Serves the pictures the DM can push onto the player projector: the curated book art committed
/// under <c>data/art/</c> plus whatever the DM has uploaded at runtime. Implemented by the
/// file-system adapter that reads <c>catalogue.json</c> from both.
/// </summary>
/// <remarks>
/// Unlike the stat library, this store is also <em>written</em> to (by the upload screen), so
/// implementations must make an added image visible to subsequent reads without a restart.
/// </remarks>
public interface IArtCatalogue
{
    /// <summary>
    /// Raised after the catalogue's contents change, so a view rendering it can re-render.
    /// </summary>
    /// <remarks>
    /// This store is a singleton read by several Blazor circuits at once, and nothing else tells
    /// them an upload appeared or was deleted. Without this a picker keeps rendering a thumbnail
    /// whose file has just been removed — the DM sees a broken image until some unrelated event
    /// happens to re-render the panel. Raised outside any internal lock, matching
    /// <c>SessionState.Changed</c>.
    /// </remarks>
    event Action Changed;

    /// <summary>Gets one image by its catalogue id.</summary>
    /// <param name="imageId">The image id ("demo-bell-warden").</param>
    /// <returns>The image, or an invalid result when the catalogue has no such id.</returns>
    Result<ArtImage> GetImage(string imageId);

    /// <summary>Every catalogued image, ordered by title.</summary>
    /// <returns>The images (possibly empty).</returns>
    IReadOnlyList<ArtImage> All();

    /// <summary>
    /// Searches the catalogue by title, subject and tags, ranked best-first, for the DM's picker.
    /// </summary>
    /// <param name="query">A substring to match; a blank query returns no matches.</param>
    /// <param name="limit">The maximum number of matches to return.</param>
    /// <returns>The ranked matches, best first (possibly empty).</returns>
    IReadOnlyList<ArtImage> Search(string query, int limit);

    /// <summary>
    /// The images that depict a given creature, by its node id — the lookup behind
    /// "suggest art for what is in the fight".
    /// </summary>
    /// <param name="nodeId">A creature's node id ("data_statblocks_monsters_vampire").</param>
    /// <returns>The images tagged with that node (possibly empty).</returns>
    IReadOnlyList<ArtImage> ForNode(string nodeId);

    /// <summary>
    /// Stores an uploaded picture and adds it to the catalogue.
    /// </summary>
    /// <param name="title">The title the DM gave it; a blank title falls back to the file name.</param>
    /// <param name="content">The raw bytes as uploaded. Validated and re-encoded, never trusted.</param>
    /// <returns>The catalogued image, or an invalid result explaining why it was refused.</returns>
    Result<ArtImage> Upload(string title, byte[] content);

    /// <summary>Removes an uploaded picture from the catalogue and deletes its file.</summary>
    /// <param name="imageId">The image id to remove; refuses ids that are not uploads.</param>
    /// <returns>OK when it was removed, otherwise an invalid result.</returns>
    Result Remove(string imageId);
}
