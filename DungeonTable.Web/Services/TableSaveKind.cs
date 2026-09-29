namespace DungeonTable.Web.Services;

/// <summary>Where the live table stands with respect to being saved.</summary>
public enum TableSaveKind
{
    /// <summary>Not determined yet.</summary>
    None = 0,

    /// <summary>No database is configured; this table will be lost when the app stops.</summary>
    Disabled = 1,

    /// <summary>Persistence is on, but nothing has been written yet this session.</summary>
    Waiting = 2,

    /// <summary>The table is saved, as of the moment on the status.</summary>
    Saved = 3,

    /// <summary>The last save attempt failed; it will be retried.</summary>
    Failed = 4,

    /// <summary>
    /// Saving is deliberately suspended: the stored document was written by a newer build than this
    /// one, and overwriting it would destroy state this build cannot read.
    /// </summary>
    Blocked = 5,
}
