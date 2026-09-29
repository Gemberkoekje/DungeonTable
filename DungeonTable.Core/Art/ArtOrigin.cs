namespace DungeonTable.Core.Art;

/// <summary>Where a catalogued image came from, which decides where its file lives and whether it
/// is under version control. The catalogue sets it from the document an entry is read from, so a
/// committed catalogue need not write it.</summary>
public enum ArtOrigin
{
    /// <summary>Not stated.</summary>
    None = 0,

    /// <summary>Committed with the content under <c>data/art/</c>: a picture from a book, or one made for the campaign.</summary>
    Book = 1,

    /// <summary>Uploaded by the DM at runtime into <c>data/art/uploads/</c>; lives in the bind mount, not in git.</summary>
    Upload = 2,
}
