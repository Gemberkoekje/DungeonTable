namespace DungeonTable.Web.Services;

/// <summary>
/// An immutable read of where the live table stands with the database, for the DM screen's status bar.
/// A DM should never have to guess whether the evening is being saved.
/// </summary>
public sealed class TableSaveStatus
{
    /// <summary>What state saving is in.</summary>
    public TableSaveKind Kind { get; init; } = TableSaveKind.None;

    /// <summary>
    /// When the table was last known to be saved. Default when it never has been — including for a
    /// stored document that was restored but has not been written to since.
    /// </summary>
    public DateTimeOffset At { get; init; }

    /// <summary>A sentence explaining the state, shown as the indicator's tooltip.</summary>
    public string Message { get; init; } = string.Empty;
}
