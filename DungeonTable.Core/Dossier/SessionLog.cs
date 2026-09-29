namespace DungeonTable.Core.Dossier;

/// <summary>
/// Every session the table has prepped, newest-relevant first. The seventh dossier tier, beside
/// the per-area, per-floor, shared-rules, campaign, party, NPC and prior-campaign ones.
/// </summary>
/// <remarks>
/// <para>
/// A list rather than a single document, so last session's plan stays readable beside the next
/// one's instead of being overwritten by it. Campaign-scoped, like the quest log and the roster.
/// </para>
/// <para>
/// Not to be confused with <c>DungeonTable.Core.Session</c>, which is the live table's own state -
/// what is revealed, who is in initiative, where the map is scrolled. This is authored prep that
/// nothing in the app writes back to.
/// </para>
/// </remarks>
public sealed class SessionLog
{
    /// <summary>The log's display title ("Session prep").</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>What this log is, in a sentence.</summary>
    public string Summary { get; init; } = string.Empty;

    /// <summary>The prepped sessions, in the order they should be shown.</summary>
    public IReadOnlyList<SessionPlan> Sessions { get; init; } = Array.Empty<SessionPlan>();
}
