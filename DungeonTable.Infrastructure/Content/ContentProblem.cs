namespace DungeonTable.Infrastructure.Content;

/// <summary>
/// Something the app skipped or dropped while reading the content: a document that does not parse, an
/// entry with no id, an id written twice, a <c>null</c> in a list, a file under a name nothing reads.
/// The stores are fail-soft, so none of these stops the table; each only leaves a tab short, which is
/// why the app says so in its log at startup.
/// </summary>
/// <param name="File">The file it is in, as the store found it.</param>
/// <param name="Problem">What happened, in a sentence: what was skipped or dropped, and why.</param>
public sealed record ContentProblem(string File, string Problem)
{
    /// <summary>
    /// True when the whole document was skipped because it could not be read. A reload that would lose
    /// a document this way keeps the content it had instead (<see cref="LiveContent"/>).
    /// </summary>
    public bool DocumentSkipped { get; init; }

    /// <summary>For a document that could not be read, why: the parser's or the file system's own message.</summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>A document that could not be read, and was skipped.</summary>
    /// <param name="file">The document.</param>
    /// <param name="reason">Why it could not be read: the parser's or the file system's own message.</param>
    /// <returns>The problem.</returns>
    public static ContentProblem Unreadable(string file, string reason) =>
        new ContentProblem(file, $"it could not be read, so the app skipped it: {reason}") { DocumentSkipped = true, Reason = reason };
}
