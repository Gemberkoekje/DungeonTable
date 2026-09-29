using DungeonTable.Application.Abstractions;
using DungeonTable.Core.Dossier;

namespace DungeonTable.Infrastructure.Content;

/// <summary>
/// The <see cref="IBookIndexStore"/> the pages see: answered by the book index of the content showing
/// now (<see cref="LiveContent.Current"/>).
/// </summary>
public sealed class LiveBookIndexStore : IBookIndexStore
{
    private readonly LiveContent live;

    /// <summary>Creates the store over the live content.</summary>
    /// <param name="live">The live content.</param>
    public LiveBookIndexStore(LiveContent live)
    {
        ArgumentNullException.ThrowIfNull(live);
        this.live = live;
    }

    /// <inheritdoc />
    public IReadOnlyList<BookIndex> AllBooks() => live.Current.Books.AllBooks();
}
