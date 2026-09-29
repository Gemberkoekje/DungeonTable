namespace DungeonTable.Infrastructure.Content;

/// <summary>Which kinds of content changed on disk, for a page to know what to read again.</summary>
[Flags]
public enum ContentChanges
{
    /// <summary>Nothing.</summary>
    None = 0,

    /// <summary>The dossiers, the stat blocks or the book index: a new <see cref="ContentSnapshot"/>.</summary>
    Documents = 1,

    /// <summary>A map's drawing (<c>.ds</c>) or its notes (<c>.regions.json</c>).</summary>
    Maps = 2,

    /// <summary>The committed art catalogue.</summary>
    Art = 4,
}
