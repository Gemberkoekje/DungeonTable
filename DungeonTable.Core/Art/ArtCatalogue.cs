namespace DungeonTable.Core.Art;

/// <summary>
/// The serialized form of an art catalogue document: <c>data/art/catalogue.json</c> for the
/// curated book art, and <c>data/art/uploads/catalogue.json</c> for whatever the DM has uploaded.
/// </summary>
public sealed class ArtCatalogue
{
    /// <summary>The images this document lists.</summary>
    public IReadOnlyList<ArtImage> Images { get; init; } = Array.Empty<ArtImage>();
}
