namespace DungeonTable.ContentTests;

/// <summary>One file in the content root that the app reads.</summary>
/// <param name="Name">Its path relative to the content root, with forward slashes: how a failure names it.</param>
/// <param name="FullPath">Its full path.</param>
/// <param name="Kind">What the app reads it as.</param>
internal sealed record ContentDocument(string Name, string FullPath, DocumentKind Kind);
