namespace DungeonTable.Infrastructure.Content;

/// <summary>What a reload of the documents did.</summary>
/// <param name="Applied">True when the new content is what the table now shows.</param>
/// <param name="Problems">What the new reading skipped or dropped, whether or not it was applied.</param>
/// <param name="Blocking">
/// When it was not applied: the documents that were read before and cannot be read now, which is why
/// the table keeps the content it had. Empty when applied.
/// </param>
/// <param name="Took">How long the reading took.</param>
public sealed record ContentReload(bool Applied, IReadOnlyList<ContentProblem> Problems, IReadOnlyList<ContentProblem> Blocking, TimeSpan Took);
