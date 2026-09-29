namespace DungeonTable.Core.Dossier;

/// <summary>
/// One thing a book mentions, in the book index: a place, a person,
/// a spell, a rule. Search finds it, a link can name it, and its card says what the book says, with a
/// page to look it up on.
/// </summary>
/// <remarks>
/// Lower-trust by design, since an index is often machine-made from the book: where the campaign has
/// its own entry with the same id (the book's Wenna Brask is the NPC <c>wenna-brask</c>), the authored
/// one wins, and this one only extends it.
/// </remarks>
public sealed class BookEntry
{
    /// <summary>Slug id, unique across every book, or the id of the authored entry it extends.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// What it is, as the conversion could tell: "spell", "feat", "item", "creature", and "lore" for
    /// anything the source did not say. A string, like a relation's kind, so an unexpected kind is shown
    /// as written rather than failing the file.
    /// </summary>
    public string Kind { get; init; } = string.Empty;

    /// <summary>The name to show ("The Old Mill").</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>What the book says about it, in a line.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>The printed page it is on, or 0 when the conversion could not tell.</summary>
    public int Page { get; init; }
}
