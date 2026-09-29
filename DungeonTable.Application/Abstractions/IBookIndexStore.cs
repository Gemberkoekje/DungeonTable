using DungeonTable.Core.Dossier;

namespace DungeonTable.Application.Abstractions;

/// <summary>
/// The book index: one <see cref="BookIndex"/> per book, read at startup and again when a book changes.
/// Search, links and cards reach the whole book through it, with the campaign's own content winning
/// wherever both have an entry.
/// </summary>
public interface IBookIndexStore
{
    /// <summary>Every loaded book, the adventure first where the files say so.</summary>
    /// <returns>The books (empty when no book index is present).</returns>
    IReadOnlyList<BookIndex> AllBooks();
}
