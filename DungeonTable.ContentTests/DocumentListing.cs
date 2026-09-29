using System.Collections.Generic;

namespace DungeonTable.ContentTests;

/// <summary>What a content root holds: the documents the app reads, and the files it would not.</summary>
/// <param name="Documents">Every document the app reads, in a stable order.</param>
/// <param name="Unread">Every <c>.json</c> file in a folder of documents that the app reads under no name.</param>
internal sealed record DocumentListing(IReadOnlyList<ContentDocument> Documents, IReadOnlyList<UnreadFile> Unread);
