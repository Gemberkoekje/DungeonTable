using DungeonTable.Core.Art;

namespace DungeonTable.Core.Session;

/// <summary>
/// The picture the DM has staged for the projector, and how it is shown. Part of
/// <see cref="RevealSnapshot"/> because it is player-visible state: a restart mid-scene should put
/// the same portrait back on the beamer rather than leave a blank frame.
/// </summary>
/// <remarks>
/// <see cref="ImageId"/> survives <see cref="Visible"/> going false on purpose. Hiding is not
/// clearing: the DM drops the picture for a beat and brings the same one straight back, so the
/// tray and the selection outlive the toggle.
/// </remarks>
public sealed class ShownArtSnapshot
{
    /// <summary>The catalogue id of the staged picture; empty when none is staged.</summary>
    public string ImageId { get; set; } = string.Empty;

    /// <summary>Whether the staged picture is currently on the projector.</summary>
    public bool Visible { get; set; }

    /// <summary>How large it is drawn.</summary>
    public ArtSize Size { get; set; }

    /// <summary>
    /// The tray: the catalogue ids the DM prepared for this scene, in the order they flip through
    /// with <c>n</c> / <c>p</c>. Stored so a restart does not cost the prep.
    /// </summary>
    public IReadOnlyList<string> TrayImageIds { get; set; } = Array.Empty<string>();
}
