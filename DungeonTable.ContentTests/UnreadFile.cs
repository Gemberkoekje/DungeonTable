using System.Collections.Generic;

namespace DungeonTable.ContentTests;

/// <summary>A <c>.json</c> file in a folder of documents that the app reads under no name.</summary>
/// <param name="Name">Its path relative to the content root.</param>
/// <param name="Patterns">The names that folder's documents go by ("level-*.json", "npcs.json").</param>
internal sealed record UnreadFile(string Name, IReadOnlyList<string> Patterns);
