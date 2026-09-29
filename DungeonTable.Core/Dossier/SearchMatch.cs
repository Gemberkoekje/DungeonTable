namespace DungeonTable.Core.Dossier;

/// <summary>
/// One search result: something the DM can open by its id. The DM panel's Search tab lists them, and
/// the Room Editor links a region, feature marker or object to one.
/// </summary>
public sealed class SearchMatch
{
    /// <summary>The <see cref="Category"/> of an area: the one kind of result a map region may link to.</summary>
    public const string AreaCategory = "area";

    /// <summary>The id to open ("data_dossiers_level_1_area_2", "bell-warden", "the-old-mill").</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Human-readable label; for an area, its title.</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>
    /// What kind of thing it is (<c>area</c>, <c>npc</c>, <c>monster</c>, an entity's kind), or
    /// <c>book</c> for an entry in the book index.
    /// </summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>
    /// Where to look it up, for a book index entry ("ALMANAC, p. 6"); for an area found by one of its
    /// block headings, the heading that matched ("The Furnace"); empty otherwise.
    /// </summary>
    public string Citation { get; init; } = string.Empty;
}
